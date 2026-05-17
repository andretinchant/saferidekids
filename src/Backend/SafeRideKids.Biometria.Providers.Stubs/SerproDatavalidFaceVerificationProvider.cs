using System;
using System.Threading;
using System.Threading.Tasks;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Stubs;

/// <summary>
/// Stub documental do Serpro Datavalid Biometria Facial. NÃO chamar — joga NotSupportedException.
///
/// Motivo do stub: Datavalid é integrado à base oficial do governo (CPF, RG) — excelente
/// para adultos, mas a cobertura biométrica de menores de idade é limitada (vínculo com
/// emissão de RG é variável por UF). Para POC de transporte ESCOLAR (crianças),
/// optamos por não usar Serpro como fonte de verdade biométrica. Fica registrado na
/// matriz como alternativa para verificação de responsáveis adultos no onboarding,
/// não para o check-in 1:1 da criança.
///
/// Ver também: docs/poc/matriz-fornecedores.md (linha Serpro Datavalid).
/// </summary>
public sealed class SerproDatavalidFaceVerificationProvider : IFaceVerificationProvider
{
    public string ProviderId => "serpro-datavalid";

    private const string Justification =
        "Stub documental — Serpro Datavalid não cobre menores de idade adequadamente. " +
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
