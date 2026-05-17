using System;
using System.Collections.Generic;

namespace SafeRideKids.Biometria.Api.Contracts;

// Motorista — endpoints 6.2 da CONTRACTS.md

public sealed record MotoristaStop(
    Guid StopId,
    Guid ChildId,
    string DisplayName,
    string ExpectedPickupTime,
    string AddressLabel,
    string BoardingMode);

public sealed record MotoristaTodayRouteResponse(
    Guid RouteId,
    DateOnly ScheduledDate,
    IReadOnlyList<MotoristaStop> Stops);

public sealed record CheckInStartRequest(Guid RouteStopId, Guid ChildId);

public sealed record CheckInStartResponse(
    Guid CheckInId,
    string ProviderId,
    LivenessSessionDto SessionInfo);

public sealed record LivenessSessionDto(
    string SessionId,
    IReadOnlyDictionary<string, string> SdkConfig,
    DateTimeOffset ExpiresAt);

public sealed record CheckInVerifyRequest(Guid CheckInId, string SessionId);

public sealed record CheckInVerifyResponse(
    string Outcome,
    double? Confidence,
    string SuggestNextAction);

public sealed record FallbackPinRequest(Guid CheckInId, string Pin);
public sealed record FallbackPinResponse(string Outcome, int AttemptsRemaining);

public sealed record FallbackManualRequest(Guid CheckInId, string Justification, string? PhotoOptionalBase64);
public sealed record FallbackManualResponse(bool Accepted);

public sealed record CheckInConfirmRequest(Guid CheckInId, decimal? GeoLat, decimal? GeoLng);
public sealed record CheckInConfirmResponse(bool Confirmed, DateTimeOffset FinishedAt);
