namespace SafeRideKids.Biometria.Core.Domain;

/// <summary>Modo de embarque da criança.</summary>
public static class BoardingMode
{
    public const string Manual = "manual";
    public const string Facial = "facial";
    public const string Tag = "tag";
}

/// <summary>Status de um enrollment biométrico.</summary>
public static class EnrollmentStatus
{
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Deleted = "deleted";
}

/// <summary>Resultado final de um check-in.</summary>
public static class CheckInResult
{
    public const string Approved = "approved";
    public const string Inconclusive = "inconclusive";
    public const string Rejected = "rejected";
    public const string Fallback = "fallback";
}

/// <summary>Tipos de fallback.</summary>
public static class FallbackType
{
    public const string Pin = "pin";
    public const string Manual = "manual";
    public const string Autorizada = "autorizada";
}

/// <summary>Tipos de ator para audit log.</summary>
public static class ActorType
{
    public const string Family = "family";
    public const string Motorista = "motorista";
    public const string Admin = "admin";
    public const string System = "system";
}

/// <summary>Sugestões de próxima ação do check-in.</summary>
public static class NextActionSuggestion
{
    public const string Confirm = "confirm";
    public const string FallbackPin = "fallback_pin";
    public const string FallbackManual = "fallback_manual";
}
