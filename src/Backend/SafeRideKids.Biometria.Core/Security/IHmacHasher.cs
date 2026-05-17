using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Security;

/// <summary>
/// Helper para hashear PII (emails, CPFs, identificadores externos) de forma
/// determinística mas irreversível antes de gravar em colunas indexáveis ou logs.
/// HMAC-SHA256 com salt por tenant (lido de ITenantSecretsProvider).
/// </summary>
public interface IHmacHasher
{
    /// <summary>Retorna hash hexadecimal lowercase (128 chars max) de <paramref name="plaintext"/> usando salt do tenant.</summary>
    Task<string> HashAsync(string tenantId, string plaintext, CancellationToken cancellationToken);
}
