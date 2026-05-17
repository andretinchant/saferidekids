using System;
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Audit;

/// <summary>
/// Logger de auditoria LGPD (tabela audit_log). Toda ação sensível
/// passa por aqui (consentimento, enrollment, check-in, DSR).
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Registra ação auditável. actorIdHash já deve vir hashado por HMAC
    /// (caller responsável). Retention_until calculado automaticamente conforme
    /// política de 6 anos (Marco Civil + ECA).
    /// </summary>
    Task LogAsync(
        string tenantId,
        string actorIdHash,
        string actorType,
        string action,
        string targetType,
        string targetId,
        string payloadJson,
        CancellationToken cancellationToken);
}
