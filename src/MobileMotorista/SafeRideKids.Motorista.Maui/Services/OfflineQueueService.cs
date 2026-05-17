using Refit;
using SafeRideKids.Motorista.Maui.Models;
using SQLite;

namespace SafeRideKids.Motorista.Maui.Services;

public interface IOfflineQueueService
{
    Task InitializeAsync();
    Task<int> EnqueueManualFallbackAsync(OfflineCheckInRecord record);
    Task<IReadOnlyList<OfflineCheckInRecord>> GetPendingAsync();
    Task<IReadOnlyList<OfflineCheckInRecord>> GetAllForTodayAsync();
    Task SyncPendingAsync(IBackendApi api, CancellationToken cancellationToken);
}

// Fila offline em SQLite. Persiste APENAS metadados — nunca imagem nem dado biometrico.
// CONTRACTS Secao 8 (LGPD): "Imagens raw: in-memory only".
public sealed class OfflineQueueService : IOfflineQueueService
{
    private SQLiteAsyncConnection? _conn;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, "saferide-motorista.db3");

    public async Task InitializeAsync()
    {
        if (_conn is not null) return;
        await _initLock.WaitAsync();
        try
        {
            if (_conn is not null) return;
            _conn = new SQLiteAsyncConnection(
                DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
            await _conn.CreateTableAsync<OfflineCheckInRecord>();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<int> EnqueueManualFallbackAsync(OfflineCheckInRecord record)
    {
        await InitializeAsync();
        return await _conn!.InsertAsync(record);
    }

    public async Task<IReadOnlyList<OfflineCheckInRecord>> GetPendingAsync()
    {
        await InitializeAsync();
        return await _conn!
            .Table<OfflineCheckInRecord>()
            .Where(r => r.Status == "pending")
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OfflineCheckInRecord>> GetAllForTodayAsync()
    {
        await InitializeAsync();
        var todayStart = DateTime.UtcNow.Date;
        return await _conn!
            .Table<OfflineCheckInRecord>()
            .Where(r => r.CreatedAtUtc >= todayStart)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task SyncPendingAsync(IBackendApi api, CancellationToken cancellationToken)
    {
        await InitializeAsync();
        var pending = await GetPendingAsync();
        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resp = await api.FallbackManualAsync(
                    new FallbackManualRequest(
                        CheckInId: item.CheckInId,
                        Justification: item.Justification,
                        PhotoBase64: null,            // foto nunca persistida; cf. LGPD
                        OfflineSynced: true),
                    cancellationToken);

                item.Status = resp.Success ? "synced" : "failed";
                item.SyncAttempts++;
                item.LastSyncAttemptUtc = DateTime.UtcNow;
                await _conn!.UpdateAsync(item);
            }
            catch (ApiException)
            {
                // Erro logico do backend — marca como falho mas mantem registro.
                item.Status = "failed";
                item.SyncAttempts++;
                item.LastSyncAttemptUtc = DateTime.UtcNow;
                await _conn!.UpdateAsync(item);
            }
            catch (HttpRequestException)
            {
                // Sem rede ainda — para por aqui, tenta na proxima vez.
                item.SyncAttempts++;
                item.LastSyncAttemptUtc = DateTime.UtcNow;
                await _conn!.UpdateAsync(item);
                break;
            }
        }
    }
}
