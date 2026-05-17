using SQLite;

namespace SafeRideKids.Motorista.Maui.Models;

// Registro persistido em SQLite para fila offline.
// LGPD: NUNCA armazenar imagem ou template biometrico aqui — apenas metadados.
// Veja CONTRACTS Secao 8 (imagens raw in-memory only).
[Table("offline_checkin")]
public sealed class OfflineCheckInRecord
{
    [PrimaryKey, AutoIncrement]
    public int LocalId { get; set; }

    [Indexed]
    public string CheckInId { get; set; } = string.Empty;

    public string RouteStopId { get; set; } = string.Empty;

    public string ChildId { get; set; } = string.Empty;

    // Justificativa manual digitada pelo motorista (modo manual offline).
    public string Justification { get; set; } = string.Empty;

    public double? GeoLat { get; set; }
    public double? GeoLng { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // 'pending' | 'synced' | 'failed'.
    public string Status { get; set; } = "pending";

    // Numero de tentativas de sincronizacao (telemetria).
    public int SyncAttempts { get; set; }

    public DateTime? LastSyncAttemptUtc { get; set; }
}
