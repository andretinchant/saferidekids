using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Unico;

/// <summary>
/// Provider biométrico via Unico IDCloud (REST).
///
/// Estrutura conforme docs.unico.io (atualizar URLs e payloads conforme contrato fechado).
/// Endpoints utilizados (placeholders):
///   - POST {base}/v1/people                          → cria subject + upload de imagens
///   - DELETE {base}/v1/people/{id}                   → remove subject (DSR-delete)
///   - POST {base}/v1/processes (type=facial-liveness)→ inicia liveness session
///   - GET  {base}/v1/processes/{id}                  → status do liveness
///   - POST {base}/v1/match                           → match 1:1 contra subject
///
/// TODO[unico-sdk-final]: revisar paths exatos, schema de request/response e error codes
/// quando contrato comercial / sandbox forem estabelecidos. SDK nativo Android/iOS
/// é injetado no MAUI separadamente.
/// </summary>
public sealed class UnicoFaceVerificationProvider : IFaceVerificationProvider
{
    public string ProviderId => "unico-idcloud";

    private readonly HttpClient _http;
    private readonly UnicoOptions _options;
    private readonly ILogger<UnicoFaceVerificationProvider> _logger;

    public UnicoFaceVerificationProvider(
        HttpClient http,
        IOptions<UnicoOptions> options,
        ILogger<UnicoFaceVerificationProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_http.BaseAddress is null) _http.BaseAddress = new Uri(_options.BaseUrl);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Images.Count is < 3 or > 5)
        {
            return new EnrollmentResult(false, string.Empty, null, "InvalidImageCount",
                $"Esperado entre 3 e 5 imagens; recebido {request.Images.Count}.");
        }

        try
        {
            // TODO[unico-sdk-final]: confirmar schema. Aqui assumimos um endpoint que aceita
            // payload JSON com images em base64 + metadados; o real pode exigir multipart.
            var payload = new UnicoCreatePersonRequest(
                ExternalId: request.ChildId,
                TenantTag: request.TenantId,
                ConsentReference: request.ConsentReferenceId,
                ImagesBase64: request.Images.Select(Convert.ToBase64String).ToArray());

            var response = await _http.PostAsJsonAsync("v1/people", payload, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Unico enroll falhou: status={Status} body={Body}", response.StatusCode, body);
                return new EnrollmentResult(false, string.Empty, null, "ProviderError", $"HTTP {(int)response.StatusCode}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<UnicoCreatePersonResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.PersonId))
            {
                return new EnrollmentResult(false, string.Empty, null, "EmptyResponse", "Resposta sem personId.");
            }

            return new EnrollmentResult(true, parsed.PersonId, EncryptedTemplate: null, ErrorCode: null, ErrorMessage: null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede Unico enroll");
            return new EnrollmentResult(false, string.Empty, null, "NetworkError", ex.Message);
        }
    }

    public async Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new UnicoCreateProcessRequest(
            Type: "facial-liveness",
            PersonId: null, // 1:1 será resolvido no /v1/match com ProviderReferenceId
            CallbackUrl: null,
            CheckInId: request.CheckInId);

        var response = await _http.PostAsJsonAsync("v1/processes", payload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content.ReadFromJsonAsync<UnicoCreateProcessResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Unico /v1/processes retornou body vazio.");

        var sdkConfig = new Dictionary<string, string>
        {
            ["processId"] = parsed.ProcessId,
            ["sdkToken"] = parsed.SdkToken ?? string.Empty,
            ["providerId"] = ProviderId
        };

        return new LivenessSessionInfo(
            SessionId: parsed.ProcessId,
            SdkConfig: sdkConfig,
            ExpiresAt: DateTimeOffset.UtcNow.Add(_options.LivenessSessionTtl));
    }

    public async Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        long cost = 0;

        try
        {
            // 1) Verifica status do processo de liveness.
            var statusResp = await _http.GetAsync($"v1/processes/{request.SessionId}", cancellationToken).ConfigureAwait(false);
            statusResp.EnsureSuccessStatusCode();
            var status = await statusResp.Content.ReadFromJsonAsync<UnicoProcessStatusResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("Unico process status vazio.");
            cost += _options.LivenessCostMicroCents;

            if (!string.Equals(status.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) || !status.LivenessPassed)
            {
                sw.Stop();
                return new VerificationResult(
                    Outcome: VerificationOutcome.Rejected,
                    Confidence: null,
                    LivenessPassed: false,
                    FailureReason: $"Liveness Unico falhou (status={status.Status}, passed={status.LivenessPassed})",
                    LatencyMs: sw.ElapsedMilliseconds,
                    ProviderCostMicroCents: cost);
            }

            // 2) Match 1:1 contra subject enrolado.
            var matchResp = await _http.PostAsJsonAsync("v1/match", new UnicoMatchRequest(
                ProcessId: request.SessionId,
                PersonId: request.ProviderReferenceId), cancellationToken).ConfigureAwait(false);
            matchResp.EnsureSuccessStatusCode();
            var match = await matchResp.Content.ReadFromJsonAsync<UnicoMatchResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Unico match vazio.");
            cost += _options.MatchCostMicroCents;

            sw.Stop();

            var sim = match.Score; // 0..100
            var outcome = sim >= _options.ApprovedThreshold
                ? VerificationOutcome.Approved
                : (sim >= _options.RejectedThreshold
                    ? VerificationOutcome.Inconclusive
                    : VerificationOutcome.Rejected);

            return new VerificationResult(
                Outcome: outcome,
                Confidence: sim / 100f,
                LivenessPassed: true,
                FailureReason: outcome == VerificationOutcome.Approved ? null : "Score Unico abaixo do threshold de Approved.",
                LatencyMs: sw.ElapsedMilliseconds,
                ProviderCostMicroCents: cost);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            sw.Stop();
            _logger.LogError(ex, "Unico VerifyAsync erro sessionId={SessionId}", request.SessionId);
            return new VerificationResult(
                Outcome: VerificationOutcome.Inconclusive,
                Confidence: null,
                LivenessPassed: false,
                FailureReason: $"Falha técnica: {ex.GetType().Name}",
                LatencyMs: sw.ElapsedMilliseconds,
                ProviderCostMicroCents: cost);
        }
    }

    public async Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerReferenceId)) return;

        try
        {
            var resp = await _http.DeleteAsync($"v1/people/{providerReferenceId}", cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Unico delete não-2xx: {Status}", resp.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Unico delete network error — tratando como sucesso idempotente");
        }
    }

    // ---- DTOs HTTP (internos ao provider; serializados via System.Text.Json) ------------

    private sealed record UnicoCreatePersonRequest(
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("tenantTag")] string TenantTag,
        [property: JsonPropertyName("consentReference")] string ConsentReference,
        [property: JsonPropertyName("images")] string[] ImagesBase64);

    private sealed record UnicoCreatePersonResponse(
        [property: JsonPropertyName("personId")] string PersonId);

    private sealed record UnicoCreateProcessRequest(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("personId")] string? PersonId,
        [property: JsonPropertyName("callbackUrl")] string? CallbackUrl,
        [property: JsonPropertyName("checkInId")] string CheckInId);

    private sealed record UnicoCreateProcessResponse(
        [property: JsonPropertyName("processId")] string ProcessId,
        [property: JsonPropertyName("sdkToken")] string? SdkToken);

    private sealed record UnicoProcessStatusResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("livenessPassed")] bool LivenessPassed);

    private sealed record UnicoMatchRequest(
        [property: JsonPropertyName("processId")] string ProcessId,
        [property: JsonPropertyName("personId")] string PersonId);

    private sealed record UnicoMatchResponse(
        [property: JsonPropertyName("score")] float Score);
}
