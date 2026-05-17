namespace SafeRideKids.Dashboard.Blazor.Models;

// Modelos de métricas. CONTRACTS.md Seção 6.3 lista o que o backend devolve;
// adicionamos comparativo lado-a-lado AWS vs Unico e série temporal,
// que são detalhes da UI (backend pode agregar tudo de uma vez).

/// <summary>
/// Resposta do endpoint <c>GET /api/v1/dashboard/metrics/summary</c>.
/// O backend agrega todos os check-ins do período e devolve agregações por provedor.
/// </summary>
public sealed record MetricsSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalCheckIns,
    IReadOnlyDictionary<string, int> ByOutcome,         // approved/inconclusive/rejected/fallback
    IReadOnlyDictionary<string, int> ByProvider,        // contagem por provedor
    long P50LatencyMs,
    long P95LatencyMs,
    long P99LatencyMs,
    double FallbackRate,                                // [0..1]
    double EstimatedCostUsd,                            // soma de microcents → USD
    IReadOnlyList<ProviderMetrics> ProviderBreakdown,   // comparativo lado-a-lado
    IReadOnlyList<DailySeriesPoint> DailySeries);       // série temporal por dia

/// <summary>Métricas agregadas por provedor (uma linha por provedor ativo).</summary>
public sealed record ProviderMetrics(
    string ProviderId,                  // aws-rekognition | unico-idcloud
    int TotalCheckIns,
    int ApprovedCount,
    int InconclusiveCount,
    int RejectedCount,
    int FallbackCount,
    double SuccessRate,                 // approved / total
    double FallbackRate,                // used_fallback / total
    long P50LatencyMs,
    long P95LatencyMs,
    long P99LatencyMs,
    double AverageConfidence,           // [0..1]
    long TotalCostMicroCents,
    double EstimatedCostUsd);

/// <summary>Ponto da série temporal diária (uma entrada por dia do período).</summary>
public sealed record DailySeriesPoint(
    DateOnly Day,
    int Approved,
    int Inconclusive,
    int Rejected,
    int Fallback);
