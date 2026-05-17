using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Providers;

/// <summary>
/// Interface canônica de provedor biométrico, conforme CONTRACTS.md Seção 3.
/// Implementações concretas: AWS Rekognition, Unico IDCloud e stubs documentais.
/// </summary>
public interface IFaceVerificationProvider
{
    /// <summary>Identificador estável do provedor. Ex.: "aws-rekognition", "unico-idcloud".</summary>
    string ProviderId { get; }

    /// <summary>
    /// Enrola N fotos da criança (3-5) e devolve referência opaca para uso posterior em verificações.
    /// Implementações NÃO devem persistir fotos raw em storage durável. Template/embedding pode
    /// ser devolvido (para o caller salvar cifrado) ou ficar armazenado no provedor (referenceId).
    /// </summary>
    Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Inicia sessão de liveness no provedor. Devolve sessionId/config que o app mobile usa
    /// para invocar o SDK nativo do provedor (ex.: Rekognition Face Liveness sessionId,
    /// Unico Process SDK configuration).
    /// </summary>
    Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Verifica resultado do liveness e faz comparação 1:1 contra o enrollment da criança esperada.
    /// Implementações fazem as duas coisas atomicamente quando o SDK suporta (Unico),
    /// ou primeiro consultam status do liveness e depois chamam CompareFaces (AWS).
    /// </summary>
    Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Apaga o enrollment no provedor (atende DSR-delete). Implementação deve ser idempotente.
    /// </summary>
    Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken);
}

/// <summary>Requisição para enrolar imagens de uma criança em um provedor biométrico.</summary>
public sealed record EnrollmentRequest(
    string ChildId,
    string TenantId,
    IReadOnlyList<byte[]> Images,            // 3-5 imagens, JPEG, max 1080x1080. Nunca persistidas em disco.
    string ConsentReferenceId                // ID do consentimento que autoriza enrollment
);

/// <summary>Resultado do enrollment biométrico.</summary>
public sealed record EnrollmentResult(
    bool Success,
    string ProviderReferenceId,              // ID opaco do provedor (ex.: AWS FaceId, Unico subjectId)
    byte[]? EncryptedTemplate,               // Opcional: alguns provedores devolvem template para o caller salvar
    string? ErrorCode,
    string? ErrorMessage
);

/// <summary>Requisição para iniciar sessão de liveness.</summary>
public sealed record LivenessSessionRequest(
    string CheckInId,
    string ChildId,
    string TenantId
);

/// <summary>Informações da sessão de liveness para uso pelo SDK mobile.</summary>
public sealed record LivenessSessionInfo(
    string SessionId,                                       // SessionId do provedor (passar ao SDK mobile)
    IReadOnlyDictionary<string, string> SdkConfig,          // Parâmetros adicionais p/ o SDK
    DateTimeOffset ExpiresAt
);

/// <summary>Requisição para verificar liveness + comparação 1:1.</summary>
public sealed record VerificationRequest(
    string SessionId,
    string ChildId,
    string TenantId,
    string ProviderReferenceId
);

/// <summary>Resultado da verificação biométrica.</summary>
public sealed record VerificationResult(
    VerificationOutcome Outcome,             // Approved, Inconclusive, Rejected
    double? Confidence,                      // 0..1 quando disponível
    bool LivenessPassed,
    string? FailureReason,
    long LatencyMs,
    long? ProviderCostMicroCents             // Custo estimado em microcentavos USD (telemetria)
);

/// <summary>Resultado lógico do match 1:1 + liveness combinados.</summary>
public enum VerificationOutcome
{
    Approved,
    Inconclusive,    // Cair em fallback obrigatoriamente
    Rejected
}
