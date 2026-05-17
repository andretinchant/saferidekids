namespace SafeRideKids.Dashboard.Blazor.Models;

// DTOs do dashboard — espelham CONTRACTS.md Seção 6.3.
// Mantemos os nomes em inglês alinhados ao backend (camelCase no JSON).
// Atenção: nenhum campo de PII clara aparece em listagens — apenas iniciais.

/// <summary>Resultado paginado genérico devolvido pelos endpoints de dashboard.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

/// <summary>
/// Resumo de check-in para listagem. Não traz nome decifrado — usar
/// <see cref="ChildDisplayInitials"/> por padrão e revelar nome só sob gesto explícito.
/// </summary>
public sealed record CheckInListItem(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Result,                 // approved | inconclusive | rejected | fallback
    string? ProviderId,             // aws-rekognition | unico-idcloud
    double? Confidence,
    bool LivenessPassed,
    bool UsedFallback,
    string? FallbackType,           // pin | manual | autorizada
    long? LatencyMs,
    long? ProviderCostMicroCents,
    Guid ChildId,
    string ChildDisplayInitials,    // ex.: "M.A.S." — default seguro
    Guid RouteId,
    Guid RouteStopId,
    string MotoristaIdHash,         // HMAC já hashado
    double? GeoLat,
    double? GeoLng);

/// <summary>Detalhe completo incluindo timeline de eventos.</summary>
public sealed record CheckInDetail(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Result,
    string? ProviderId,
    string? ProviderSessionId,
    double? Confidence,
    bool LivenessPassed,
    bool UsedFallback,
    string? FallbackType,
    long? LatencyMs,
    long? ProviderCostMicroCents,
    double? GeoLat,
    double? GeoLng,
    string? Notes,
    Guid ChildId,
    string ChildDisplayInitials,
    Guid RouteId,
    Guid RouteStopId,
    string MotoristaIdHash,
    IReadOnlyList<CheckInEvent> Events);

public sealed record CheckInEvent(
    Guid Id,
    string EventType,             // ex.: StartLiveness, LivenessFailed, VerifyOk, FallbackTriggered
    DateTimeOffset OccurredAt,
    string PayloadJson);          // payload JSONB serializado

/// <summary>Filtros para a listagem de check-ins (Seção 6.3 do CONTRACTS).</summary>
public sealed record CheckInQuery(
    DateTime? From,
    DateTime? To,
    string? ProviderId,
    string? Outcome,
    bool? UsedFallback,
    int Page = 1,
    int PageSize = 25);

/// <summary>
/// Resultado de DSR-access ou portability: devolve um envelope com payload
/// estruturado para o operador exportar. Para DSR-delete, devolve apenas
/// confirmação + lista de provedores impactados.
/// </summary>
public sealed record DsrResponse(
    string Action,                // access | delete | portability
    string Status,                // accepted | completed
    Guid? RequestId,
    string? PayloadJson,
    IReadOnlyList<string>? AffectedProviders,
    DateTimeOffset RequestedAt);

/// <summary>Solicitação DSR enviada pelo dashboard ao backend.</summary>
public sealed record DsrRequest(
    string SubjectType,           // family | child
    Guid SubjectId,
    string Justification,         // obrigatório para delete (LGPD)
    string OperatorIdHash);       // HMAC do operador autenticado (auditoria)
