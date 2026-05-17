namespace SafeRideKids.Biometria.Providers.Unico;

/// <summary>Opções de configuração do provider Unico IDCloud.</summary>
public sealed class UnicoOptions
{
    /// <summary>Base URL da API Unico. Ex.: https://api.unico.io</summary>
    public string BaseUrl { get; set; } = "https://api.unico.io";

    /// <summary>Header Authorization (Bearer + token). Resolvido em runtime a partir de Secrets Manager.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Threshold para Approved (0..100).</summary>
    public float ApprovedThreshold { get; set; } = 90f;

    /// <summary>Threshold para Rejected (0..100). Entre Rejected e Approved → Inconclusive.</summary>
    public float RejectedThreshold { get; set; } = 70f;

    /// <summary>Custo estimado por chamada de match em microcentavos USD (placeholder até contrato fechado).</summary>
    public long MatchCostMicroCents { get; set; } = 350_000L;

    /// <summary>Custo estimado por liveness session em microcentavos USD (placeholder).</summary>
    public long LivenessCostMicroCents { get; set; } = 500_000L;

    /// <summary>TTL da liveness session (default Unico ~ 5 min).</summary>
    public TimeSpan LivenessSessionTtl { get; set; } = TimeSpan.FromMinutes(5);
}
