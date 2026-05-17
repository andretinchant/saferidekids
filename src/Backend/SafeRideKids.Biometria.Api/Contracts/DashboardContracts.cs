using System;
using System.Collections.Generic;

namespace SafeRideKids.Biometria.Api.Contracts;

// Dashboard — endpoints 6.3 da CONTRACTS.md

public sealed record CheckInListItem(
    Guid CheckInId,
    Guid ChildId,
    string? Result,
    string? ProviderId,
    double? Confidence,
    bool UsedFallback,
    int? LatencyMs,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record CheckInPagedResponse(
    IReadOnlyList<CheckInListItem> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CheckInEventDto(
    string EventType,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record CheckInDetailResponse(
    CheckInListItem CheckIn,
    IReadOnlyList<CheckInEventDto> Events);

public sealed record OutcomeCount(string Outcome, int Count);
public sealed record ProviderCount(string ProviderId, int Count, long EstimatedCostMicroCents);

public sealed record MetricsSummaryResponse(
    int TotalCheckIns,
    IReadOnlyList<OutcomeCount> ByOutcome,
    IReadOnlyList<ProviderCount> ByProvider,
    long? P50LatencyMs,
    long? P95LatencyMs,
    double FallbackRate,
    decimal EstimatedCostUsd);

public sealed record FamilySummary(
    Guid FamilyId,
    string TenantId,
    string? PreferredProviderId,
    DateTimeOffset CreatedAt);

public sealed record DsrRequest(string TargetType, string TargetId);
public sealed record DsrResponse(string Action, string Outcome, string? Details);
