using System.Text.Json.Serialization;

namespace SafeRideKids.Motorista.Maui.Models;

// DTOs do app motorista. Espelham CONTRACTS.md Secao 6.2.
// Naming: PascalCase em C#; com [JsonPropertyName] para garantir camelCase no fio.

// ============ Autenticacao ============

public sealed record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("tenantId")] string TenantId);

public sealed record LoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string? RefreshToken,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("motoristaId")] string MotoristaId,
    [property: JsonPropertyName("displayName")] string DisplayName);

// ============ Rota do dia (GET /api/v1/motorista/route/today) ============

public sealed record TodayRouteResponse(
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("scheduledDate")] DateOnly ScheduledDate,
    [property: JsonPropertyName("stops")] IReadOnlyList<RouteStopDto> Stops);

public sealed record RouteStopDto(
    [property: JsonPropertyName("stopId")] string StopId,
    [property: JsonPropertyName("childId")] string ChildId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("expectedPickupTime")] TimeOnly ExpectedPickupTime,
    [property: JsonPropertyName("addressLabel")] string AddressLabel,
    [property: JsonPropertyName("boardingMode")] string BoardingMode);

// ============ Inicio do check-in (POST /api/v1/motorista/checkin/start) ============

public sealed record CheckInStartRequest(
    [property: JsonPropertyName("routeStopId")] string RouteStopId,
    [property: JsonPropertyName("childId")] string ChildId);

public sealed record CheckInStartResponse(
    [property: JsonPropertyName("checkInId")] string CheckInId,
    [property: JsonPropertyName("providerId")] string ProviderId,
    [property: JsonPropertyName("sessionInfo")] LivenessSessionInfoDto SessionInfo);

public sealed record LivenessSessionInfoDto(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("sdkConfig")] IReadOnlyDictionary<string, string> SdkConfig,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

// ============ Verificacao (POST /api/v1/motorista/checkin/verify) ============

public sealed record CheckInVerifyRequest(
    [property: JsonPropertyName("checkInId")] string CheckInId,
    [property: JsonPropertyName("sessionId")] string SessionId);

public sealed record CheckInVerifyResponse(
    [property: JsonPropertyName("outcome")] string Outcome,                   // "approved" | "inconclusive" | "rejected"
    [property: JsonPropertyName("confidence")] double? Confidence,
    [property: JsonPropertyName("suggestNextAction")] string SuggestNextAction); // "confirm" | "fallback_pin" | "fallback_manual"

// ============ Fallback PIN (POST /api/v1/motorista/checkin/fallback/pin) ============

public sealed record FallbackPinRequest(
    [property: JsonPropertyName("checkInId")] string CheckInId,
    [property: JsonPropertyName("pin")] string Pin);

public sealed record FallbackPinResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("attemptsLeft")] int? AttemptsLeft,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage);

// ============ Fallback manual (POST /api/v1/motorista/checkin/fallback/manual) ============

// Foto opcional vai como base64 no JSON. Em producao usar multipart/form-data;
// para a POC mantemos JSON simples — o tamanho da foto fica limitado pelo backend.
public sealed record FallbackManualRequest(
    [property: JsonPropertyName("checkInId")] string CheckInId,
    [property: JsonPropertyName("justification")] string Justification,
    [property: JsonPropertyName("photoBase64")] string? PhotoBase64,
    [property: JsonPropertyName("offlineSynced")] bool OfflineSynced);

public sealed record FallbackManualResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage);

// ============ Confirmar (POST /api/v1/motorista/checkin/confirm) ============

public sealed record CheckInConfirmRequest(
    [property: JsonPropertyName("checkInId")] string CheckInId,
    [property: JsonPropertyName("geoLat")] double? GeoLat,
    [property: JsonPropertyName("geoLng")] double? GeoLng);

public sealed record CheckInConfirmResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("finalStatus")] string FinalStatus);
