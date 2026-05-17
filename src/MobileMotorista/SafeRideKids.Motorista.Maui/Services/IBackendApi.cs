using Refit;
using SafeRideKids.Motorista.Maui.Models;

namespace SafeRideKids.Motorista.Maui.Services;

// Cliente Refit para os endpoints listados em CONTRACTS.md Secao 6.2.
// Apenas o que o motorista precisa; outros agentes consomem seus proprios contratos.
public interface IBackendApi
{
    // Login: nao consta literalmente no CONTRACTS Secao 6 (foco em familia/motorista/dashboard).
    // Para a POC, mock local em AuthService; mantemos o endpoint aqui como placeholder
    // para integracao real com Cognito (CONTRACTS Secao 13).
    [Post("/api/v1/auth/motorista/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest body, CancellationToken ct);

    // GET /api/v1/motorista/route/today — rota do dia.
    [Get("/api/v1/motorista/route/today")]
    Task<TodayRouteResponse> GetTodayRouteAsync(CancellationToken ct);

    // POST /api/v1/motorista/checkin/start.
    [Post("/api/v1/motorista/checkin/start")]
    Task<CheckInStartResponse> StartCheckInAsync([Body] CheckInStartRequest body, CancellationToken ct);

    // POST /api/v1/motorista/checkin/verify.
    [Post("/api/v1/motorista/checkin/verify")]
    Task<CheckInVerifyResponse> VerifyCheckInAsync([Body] CheckInVerifyRequest body, CancellationToken ct);

    // POST /api/v1/motorista/checkin/fallback/pin.
    [Post("/api/v1/motorista/checkin/fallback/pin")]
    Task<FallbackPinResponse> FallbackPinAsync([Body] FallbackPinRequest body, CancellationToken ct);

    // POST /api/v1/motorista/checkin/fallback/manual.
    [Post("/api/v1/motorista/checkin/fallback/manual")]
    Task<FallbackManualResponse> FallbackManualAsync([Body] FallbackManualRequest body, CancellationToken ct);

    // POST /api/v1/motorista/checkin/confirm.
    [Post("/api/v1/motorista/checkin/confirm")]
    Task<CheckInConfirmResponse> ConfirmCheckInAsync([Body] CheckInConfirmRequest body, CancellationToken ct);
}
