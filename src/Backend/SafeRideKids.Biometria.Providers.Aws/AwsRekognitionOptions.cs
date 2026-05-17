namespace SafeRideKids.Biometria.Providers.Aws;

/// <summary>
/// Opções de configuração do provider AWS Rekognition.
/// </summary>
public sealed class AwsRekognitionOptions
{
    /// <summary>Threshold de confidence (0..100) acima do qual o match é Approved.</summary>
    public float ApprovedThreshold { get; set; } = 90f;

    /// <summary>Threshold abaixo do qual o match é Rejected. Entre Rejected e Approved → Inconclusive.</summary>
    public float RejectedThreshold { get; set; } = 70f;

    /// <summary>
    /// Template do nome da collection do Rekognition por tenant.
    /// Substituído por tenantId em runtime.
    /// </summary>
    public string CollectionNameTemplate { get; set; } = "poc_tenant_{tenantId}";

    /// <summary>Tempo de vida da liveness session (default Rekognition ~ 3 min).</summary>
    public TimeSpan LivenessSessionTtl { get; set; } = TimeSpan.FromMinutes(3);

    // Custos AWS Rekognition (aproximados, US$ → microcentavos = USD * 1_000_000_000 / 1000).
    // 1 microcent = 0.000001 cent = 1e-8 USD. Tabela tipo:
    //   IndexFaces    → ~$0.001 = 100_000 microcents
    //   FaceLiveness  → ~$0.0025 = 250_000 microcents
    //   CompareFaces  → ~$0.001 = 100_000 microcents
    //   SearchFaces   → ~$0.001 = 100_000 microcents
    public long IndexFacesCostMicroCents { get; set; } = 100_000L;
    public long FaceLivenessCostMicroCents { get; set; } = 250_000L;
    public long CompareFacesCostMicroCents { get; set; } = 100_000L;
}
