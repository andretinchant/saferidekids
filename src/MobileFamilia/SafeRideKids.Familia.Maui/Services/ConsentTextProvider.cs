using System.Security.Cryptography;
using System.Text;

namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Carrega o texto de consentimento LGPD versionado do bundle (Resources/Raw).
/// O hash SHA-256 do texto exato exibido vai junto da assinatura — auditoria
/// posterior comprova qual versão o usuário aceitou (CONTRACTS Seção 8).
/// </summary>
public interface IConsentTextProvider
{
    Task<ConsentText> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record ConsentText(
    string Version,
    string Body,
    string Sha256Hash);

public sealed class ConsentTextProvider : IConsentTextProvider
{
    // Por enquanto carregamos apenas a v1. Quando atualizar o texto, criar
    // consent-text-v2.txt e ajustar CurrentVersion — o backend rejeita
    // consents com hash desconhecido.
    private const string CurrentVersion = "v1";
    private const string AssetFileName = "consent-text-v1.txt";

    private ConsentText? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ConsentText> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetFileName).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            var hash = ComputeSha256Hex(body);
            _cached = new ConsentText(CurrentVersion, body, hash);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
