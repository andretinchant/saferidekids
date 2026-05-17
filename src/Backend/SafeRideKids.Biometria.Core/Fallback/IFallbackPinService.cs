using System;
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Fallback;

/// <summary>
/// Geração e validação de PIN de fallback (6 dígitos, 24h, 3 tentativas).
/// Conforme CONTRACTS.md Seção 7.
/// </summary>
public interface IFallbackPinService
{
    /// <summary>
    /// Gera um novo PIN para a criança (revoga PIN ativo anterior se houver).
    /// Retorna o PIN claro (6 dígitos) para envio ao responsável via canal de notificação.
    /// O PIN claro NUNCA é persistido — apenas o HMAC.
    /// </summary>
    Task<GeneratePinResult> GeneratePinAsync(string tenantId, Guid childId, CancellationToken cancellationToken);

    /// <summary>
    /// Valida PIN fornecido pelo motorista. Conta tentativa, invalida após 3 falhas,
    /// marca como consumido em sucesso. Retorna resultado tipado (sem exceção em falha de negócio).
    /// </summary>
    Task<PinValidationResult> ValidatePinAsync(string tenantId, Guid childId, string pin, CancellationToken cancellationToken);
}

/// <summary>Resultado da geração de PIN.</summary>
public sealed record GeneratePinResult(
    string PlaintextPin,           // Apenas para envio imediato; não persistir
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil);

/// <summary>Resultado da validação de PIN.</summary>
public sealed record PinValidationResult(
    PinValidationOutcome Outcome,
    int AttemptsRemaining);

/// <summary>Resultados possíveis de validação de PIN.</summary>
public enum PinValidationOutcome
{
    Valid,
    Invalid,
    Expired,
    Blocked,        // 3 tentativas atingidas
    NotFound,
    AlreadyConsumed
}
