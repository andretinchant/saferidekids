using Refit;
using SafeRideKids.Dashboard.Blazor.Models;

namespace SafeRideKids.Dashboard.Blazor.Services;

/// <summary>
/// Interface Refit do dashboard contra o backend. Cobre os endpoints da
/// Seção 6.3 do CONTRACTS.md. Todas as chamadas requerem Bearer JWT do
/// Cognito injetado via <see cref="BearerTokenHandler"/>.
/// </summary>
public interface IBackendApi
{
    // ----------------------------------------------------------------------------------
    // Check-ins
    // ----------------------------------------------------------------------------------

    /// <summary>Listagem paginada de check-ins com filtros opcionais.</summary>
    [Get("/api/v1/dashboard/checkins")]
    Task<PagedResult<CheckInListItem>> GetCheckInsAsync(
        [Query] DateTime? from,
        [Query] DateTime? to,
        [Query] string? providerId,
        [Query] string? outcome,
        [Query] bool? usedFallback,
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Detalhe completo do check-in com timeline de eventos.</summary>
    [Get("/api/v1/dashboard/checkins/{id}")]
    Task<CheckInDetail> GetCheckInDetailAsync(
        Guid id,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // Métricas
    // ----------------------------------------------------------------------------------

    /// <summary>Agregações para a página Métricas — período obrigatório.</summary>
    [Get("/api/v1/dashboard/metrics/summary")]
    Task<MetricsSummary> GetMetricsSummaryAsync(
        [Query] DateTime from,
        [Query] DateTime to,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // Famílias / crianças (revisão operacional)
    // ----------------------------------------------------------------------------------

    /// <summary>Lista famílias paginadas — devolve hashes/iniciais, nunca PII clara.</summary>
    [Get("/api/v1/dashboard/families")]
    Task<PagedResult<FamilyListItem>> GetFamiliesAsync(
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Lista crianças de uma família com status de enrollment por provedor.</summary>
    [Get("/api/v1/dashboard/families/{familyId}/children")]
    Task<IReadOnlyList<ChildListItem>> GetChildrenByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revela o display_name decifrado da criança. Esta operação gera entry
    /// em <c>audit_log</c> no backend (logged-as-viewed). Usar com gesto explícito.
    /// </summary>
    [Get("/api/v1/dashboard/children/{childId}/display-name")]
    Task<RevealedDisplayName> RevealChildDisplayNameAsync(
        Guid childId,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // DSR (Data Subject Request)
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// Executa solicitação DSR. <c>action</c> ∈ {access, delete, portability}.
    /// Backend valida justificativa (obrigatória para delete) e dispara o
    /// processamento — para a POC, sincrono; produção, fila + SLA 7d.
    /// </summary>
    [Post("/api/v1/dashboard/dsr/{action}")]
    Task<DsrResponse> SubmitDsrAsync(
        string action,
        [Body] DsrRequest request,
        CancellationToken cancellationToken);
}
