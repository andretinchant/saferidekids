using System;
using System.Threading;
using System.Threading.Tasks;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Stubs;

/// <summary>
/// Stub documental do FaceTec ZoOm. NÃO chamar — joga NotSupportedException.
///
/// Motivo do stub: FaceTec é vendido em contrato enterprise (sem self-service / sem
/// sandbox público). Para integrar de fato precisamos de licença + SDK assinado.
/// Decisão de POC: avaliar comercialmente em fase posterior; aqui ficamos só com
/// a entrada documental na matriz para comparação técnica/custo.
///
/// Ver também: docs/poc/matriz-fornecedores.md (linha FaceTec ZoOm).
/// </summary>
public sealed class FaceTecFaceVerificationProvider : IFaceVerificationProvider
{
    public string ProviderId => "facetec-zoom";

    private const string Justification =
        "Stub documental — FaceTec requer contrato enterprise (sem self-service). " +
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
