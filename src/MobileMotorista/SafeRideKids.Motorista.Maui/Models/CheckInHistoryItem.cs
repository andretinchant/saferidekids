namespace SafeRideKids.Motorista.Maui.Models;

// View-model auxiliar para a tela de historico do dia.
// Construido a partir do que aconteceu na sessao (in-memory) + offline queue (SQLite).
public sealed class CheckInHistoryItem
{
    public string CheckInId { get; init; } = string.Empty;
    public string ChildDisplayName { get; init; } = string.Empty;
    public string AddressLabel { get; init; } = string.Empty;
    public DateTime OccurredAtLocal { get; init; }
    public CheckInOutcome? Outcome { get; init; }
    public bool UsedFallback { get; init; }
    public string? FallbackType { get; init; } // "pin" | "manual"
    public bool SyncedToBackend { get; init; } = true;

    // Cor de status para o XAML (usa Style por chave).
    public string StatusKey => (Outcome, UsedFallback, SyncedToBackend) switch
    {
        (CheckInOutcome.Approved, false, true) => "StatusApproved",
        (CheckInOutcome.Approved, true, true) => "StatusFallbackOk",
        (CheckInOutcome.Rejected, _, _) => "StatusRejected",
        (CheckInOutcome.Inconclusive, _, _) => "StatusInconclusive",
        (_, _, false) => "StatusPendingSync",
        _ => "StatusUnknown"
    };

    public string StatusLabel => (Outcome, UsedFallback, SyncedToBackend) switch
    {
        (CheckInOutcome.Approved, false, true) => "Aprovado",
        (CheckInOutcome.Approved, true, true) => "Aprovado via " + (FallbackType == "pin" ? "PIN" : "manual"),
        (CheckInOutcome.Rejected, _, _) => "Rejeitado",
        (CheckInOutcome.Inconclusive, _, _) => "Inconclusivo",
        (_, _, false) => "Aguardando sincronizacao",
        _ => "Desconhecido"
    };
}
