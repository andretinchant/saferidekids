using System;
using System.Threading;
using System.Threading.Tasks;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Stubs;

/// <summary>
/// Stub documental do Azure AI Face. NÃO chamar — joga NotSupportedException.
///
/// Motivo do stub: a Microsoft restringiu o serviço Azure AI Face em 2022 com Limited Access:
/// reconhecimento facial 1:1/1:N e identificação requerem aprovação prévia via formulário,
/// com revisão de casos de uso (compliance / mitigação de risco). Como a POC quer responder
/// rápido sobre viabilidade técnica, ficamos só com a entrada documental na
/// matriz-fornecedores.md sem código de integração.
///
/// Ver também: docs/poc/matriz-fornecedores.md (linha Azure AI Face).
/// </summary>
public sealed class AzureFaceFaceVerificationProvider : IFaceVerificationProvider
{
    public string ProviderId => "azure-face";

    private const string Justification =
        "Stub documental — Azure AI Face requer aprovação Limited Access (Microsoft Responsible AI). " +
        "Ver docs/poc/matriz-fornecedores.md.";

    public Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Justification);

    public Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Justification);

    public Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Justification);

    public Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Justification);
}
