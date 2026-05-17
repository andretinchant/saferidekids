namespace SafeRideKids.Motorista.Maui.Models;

// Enum local do app espelhando os 3 outcomes possiveis vindos do backend.
// String -> enum em camada de servico para isolar o resto do app de variacao do contrato.
public enum CheckInOutcome
{
    Approved,
    Inconclusive,
    Rejected
}

public enum CheckInSuggestedAction
{
    Confirm,
    FallbackPin,
    FallbackManual
}

// Estado da maquina do check-in, espelha a UX state machine do prompt.
public enum CheckInState
{
    Idle,
    StartingCheckIn,
    CapturingLiveness,
    Verifying,
    FallbackPin,
    FallbackManual,
    Confirming,
    Done,
    NetworkError
}

public static class CheckInOutcomeExtensions
{
    public static CheckInOutcome ParseOutcome(string outcome) => outcome?.ToLowerInvariant() switch
    {
        "approved" => CheckInOutcome.Approved,
        "rejected" => CheckInOutcome.Rejected,
        _ => CheckInOutcome.Inconclusive
    };

    public static CheckInSuggestedAction ParseAction(string action) => action?.ToLowerInvariant() switch
    {
        "confirm" => CheckInSuggestedAction.Confirm,
        "fallback_pin" => CheckInSuggestedAction.FallbackPin,
        _ => CheckInSuggestedAction.FallbackManual
    };
}
