using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Multitenancy;

/// <summary>
/// Provedor de segredos por tenant (salt HMAC, chaves de criptografia,
/// credenciais de provedores externos). Backed por AWS Secrets Manager
/// em produção; in-memory nos testes.
/// </summary>
public interface ITenantSecretsProvider
{
    /// <summary>Salt HMAC-SHA256 para hashear PII em logs/colunas hashadas.</summary>
    Task<byte[]> GetHmacSaltAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Chave simétrica para pgp_sym_encrypt do PostgreSQL (PII em colunas).</summary>
    Task<string> GetPgEncryptionKeyAsync(string tenantId, CancellationToken cancellationToken);
}
