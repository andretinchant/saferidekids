using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Aws;

/// <summary>
/// Provider biométrico via AWS Rekognition Face Liveness + Face Collections.
///
/// Fluxo:
///   - EnrollAsync       → IndexFaces em collection por tenant. providerReferenceId = primeiro FaceId persistente.
///   - StartLivenessAsync→ CreateFaceLivenessSession (sessão de até ~3 min).
///   - VerifyAsync       → GetFaceLivenessSessionResults (LivenessPassed?), depois SearchFacesByImage
///                         da reference image (devolvida pelo serviço) contra a collection do tenant
///                         → match contra o FaceId enrolado da criança.
///   - DeleteEnrollment  → DeleteFaces na collection.
///
/// IMPORTANTE LGPD: IndexFaces armazena vetor (embedding) gerenciado pela AWS dentro da collection.
/// Nenhuma foto raw é persistida pelo nosso código. Reference image do liveness é gerenciada
/// pela AWS no S3 interno do Rekognition; usamos apenas para o SearchFaces e descartamos.
/// </summary>
public sealed class AwsRekognitionFaceVerificationProvider : IFaceVerificationProvider
{
    public string ProviderId => "aws-rekognition";

    private readonly IAmazonRekognition _rekognition;
    private readonly AwsRekognitionOptions _options;
    private readonly ILogger<AwsRekognitionFaceVerificationProvider> _logger;

    public AwsRekognitionFaceVerificationProvider(
        IAmazonRekognition rekognition,
        IOptions<AwsRekognitionOptions> options,
        ILogger<AwsRekognitionFaceVerificationProvider> logger)
    {
        _rekognition = rekognition ?? throw new ArgumentNullException(nameof(rekognition));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Images.Count is < 3 or > 5)
        {
            return new EnrollmentResult(false, string.Empty, null, "InvalidImageCount",
                $"Esperado entre 3 e 5 imagens; recebido {request.Images.Count}.");
        }

        var collection = ResolveCollectionName(request.TenantId);
        await EnsureCollectionAsync(collection, cancellationToken).ConfigureAwait(false);

        string? primaryFaceId = null;

        foreach (var image in request.Images)
        {
            var indexResponse = await _rekognition.IndexFacesAsync(new IndexFacesRequest
            {
                CollectionId = collection,
                Image = new Image { Bytes = new MemoryStream(image) },
                ExternalImageId = request.ChildId,
                DetectionAttributes = new List<string> { "DEFAULT" },
                MaxFaces = 1,
                QualityFilter = QualityFilter.AUTO
            }, cancellationToken).ConfigureAwait(false);

            var face = indexResponse.FaceRecords?.FirstOrDefault();
            if (face?.Face?.FaceId is null)
            {
                _logger.LogWarning("IndexFaces não retornou face — qualidade baixa? childIdPrefix={Prefix}",
                    request.ChildId[..Math.Min(4, request.ChildId.Length)]);
                continue;
            }

            primaryFaceId ??= face.Face.FaceId;
        }

        if (primaryFaceId is null)
        {
            return new EnrollmentResult(false, string.Empty, null, "NoFaceDetected",
                "Nenhuma face de qualidade suficiente detectada nas imagens enviadas.");
        }

        return new EnrollmentResult(true, primaryFaceId, EncryptedTemplate: null, ErrorCode: null, ErrorMessage: null);
    }

    public async Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _rekognition.CreateFaceLivenessSessionAsync(new CreateFaceLivenessSessionRequest
        {
            // ClientRequestToken permite idempotência para retries da mesma checkin.
            ClientRequestToken = request.CheckInId
        }, cancellationToken).ConfigureAwait(false);

        var sdkConfig = new Dictionary<string, string>
        {
            ["sessionId"] = response.SessionId,
            ["region"] = _options.GetRegionLabel(),
            ["providerId"] = ProviderId
        };

        return new LivenessSessionInfo(
            SessionId: response.SessionId,
            SdkConfig: sdkConfig,
            ExpiresAt: DateTimeOffset.UtcNow.Add(_options.LivenessSessionTtl));
    }

    public async Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        long costMicroCents = 0;

        try
        {
            var liveness = await _rekognition.GetFaceLivenessSessionResultsAsync(
                new GetFaceLivenessSessionResultsRequest { SessionId = request.SessionId }, cancellationToken).ConfigureAwait(false);
            costMicroCents += _options.FaceLivenessCostMicroCents;

            // Aprovação de liveness: confidence ≥ 70 é o threshold padrão da AWS.
            var livenessPassed = liveness.Confidence >= 70f && liveness.Status == LivenessSessionStatus.SUCCEEDED;

            if (!livenessPassed)
            {
                sw.Stop();
                return new VerificationResult(
                    Outcome: VerificationOutcome.Rejected,
                    Confidence: liveness.Confidence / 100f,
                    LivenessPassed: false,
                    FailureReason: $"Liveness failed (status={liveness.Status}, conf={liveness.Confidence})",
                    LatencyMs: sw.ElapsedMilliseconds,
                    ProviderCostMicroCents: costMicroCents);
            }

            // Reference image vem do liveness — usar para SearchFacesByImage na collection do tenant.
            if (liveness.ReferenceImage?.Bytes is null)
            {
                sw.Stop();
                return new VerificationResult(
                    Outcome: VerificationOutcome.Inconclusive,
                    Confidence: null,
                    LivenessPassed: true,
                    FailureReason: "Reference image ausente do liveness response.",
                    LatencyMs: sw.ElapsedMilliseconds,
                    ProviderCostMicroCents: costMicroCents);
            }

            var collection = ResolveCollectionName(request.TenantId);
            var search = await _rekognition.SearchFacesByImageAsync(new SearchFacesByImageRequest
            {
                CollectionId = collection,
                Image = new Image { Bytes = liveness.ReferenceImage.Bytes },
                FaceMatchThreshold = _options.RejectedThreshold,
                MaxFaces = 1
            }, cancellationToken).ConfigureAwait(false);
            costMicroCents += _options.CompareFacesCostMicroCents;

            sw.Stop();

            var topMatch = search.FaceMatches?.FirstOrDefault();
            if (topMatch is null || topMatch.Face.FaceId != request.ProviderReferenceId)
            {
                // Match não bateu com o FaceId esperado da criança → 1:1 falhou.
                return new VerificationResult(
                    Outcome: VerificationOutcome.Rejected,
                    Confidence: (topMatch?.Similarity / 100f),
                    LivenessPassed: true,
                    FailureReason: "Top match não corresponde ao FaceId enrolado para esta criança.",
                    LatencyMs: sw.ElapsedMilliseconds,
                    ProviderCostMicroCents: costMicroCents);
            }

            var sim = topMatch.Similarity;
            var outcome = sim >= _options.ApprovedThreshold
                ? VerificationOutcome.Approved
                : (sim >= _options.RejectedThreshold
                    ? VerificationOutcome.Inconclusive
                    : VerificationOutcome.Rejected);

            return new VerificationResult(
                Outcome: outcome,
                Confidence: sim / 100f,
                LivenessPassed: true,
                FailureReason: outcome == VerificationOutcome.Approved ? null : "Confidence abaixo do threshold de Approved.",
                LatencyMs: sw.ElapsedMilliseconds,
                ProviderCostMicroCents: costMicroCents);
        }
        catch (Exception ex) when (ex is AmazonRekognitionException or InvalidOperationException)
        {
            sw.Stop();
            _logger.LogError(ex, "Erro Rekognition em VerifyAsync sessionId={SessionId}", request.SessionId);
            return new VerificationResult(
                Outcome: VerificationOutcome.Inconclusive,
                Confidence: null,
                LivenessPassed: false,
                FailureReason: $"Falha técnica: {ex.GetType().Name}",
                LatencyMs: sw.ElapsedMilliseconds,
                ProviderCostMicroCents: costMicroCents);
        }
    }

    public async Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerReferenceId)) return;

        // Como o caller só passa o FaceId (referenceId), precisamos descobrir a collection.
        // Idempotência: DeleteFaces tolera FaceId inexistente (não lança).
        // Estratégia: tentar em todas collections conhecidas do processo. Para POC,
        // limitamos a 1 collection passada via env BIOMETRIA_DEFAULT_TENANT_COLLECTION
        // ou via convention; aqui apenas tentamos a lista de collections atual.
        try
        {
            var collections = await _rekognition.ListCollectionsAsync(new ListCollectionsRequest(), cancellationToken).ConfigureAwait(false);
            foreach (var c in collections.CollectionIds ?? Enumerable.Empty<string>())
            {
                try
                {
                    await _rekognition.DeleteFacesAsync(new DeleteFacesRequest
                    {
                        CollectionId = c,
                        FaceIds = new List<string> { providerReferenceId }
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ResourceNotFoundException)
                {
                    // Esperado: FaceId pode existir só em uma collection.
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha em DeleteEnrollmentAsync — pode ser idempotente, continuando.");
        }
    }

    private async Task EnsureCollectionAsync(string collection, CancellationToken cancellationToken)
    {
        try
        {
            await _rekognition.DescribeCollectionAsync(
                new DescribeCollectionRequest { CollectionId = collection }, cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceNotFoundException)
        {
            await _rekognition.CreateCollectionAsync(
                new CreateCollectionRequest { CollectionId = collection }, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Collection criada: {Collection}", collection);
        }
    }

    private string ResolveCollectionName(string tenantId) =>
        _options.CollectionNameTemplate.Replace("{tenantId}", SanitizeTenantId(tenantId), StringComparison.Ordinal);

    private static string SanitizeTenantId(string tenantId)
    {
        // Rekognition CollectionId aceita [a-zA-Z0-9_.\-]+ (até 255 chars).
        Span<char> buf = stackalloc char[tenantId.Length];
        for (int i = 0; i < tenantId.Length; i++)
        {
            var c = tenantId[i];
            buf[i] = (char.IsLetterOrDigit(c) || c is '_' or '.' or '-') ? c : '_';
        }
        return new string(buf);
    }
}

internal static class AwsRekognitionOptionsExtensions
{
    /// <summary>Retorna a região AWS atual (env var ou default us-east-1).</summary>
    public static string GetRegionLabel(this AwsRekognitionOptions _) =>
        Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
}
