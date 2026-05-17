using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Security;

namespace SafeRideKids.Biometria.Infrastructure.Security;

/// <summary>
/// Implementação HMAC-SHA256 com salt por tenant. Salt vem de
/// ITenantSecretsProvider (AWS Secrets Manager em prod).
/// </summary>
public sealed class HmacHasher : IHmacHasher
{
    private readonly ITenantSecretsProvider _secretsProvider;

    public HmacHasher(ITenantSecretsProvider secretsProvider)
    {
        _secretsProvider = secretsProvider ?? throw new ArgumentNullException(nameof(secretsProvider));
    }

    public async Task<string> HashAsync(string tenantId, string plaintext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId vazio.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(plaintext);

        var salt = await _secretsProvider.GetHmacSaltAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext.Trim().ToLowerInvariant());

        using var hmac = new HMACSHA256(salt);
        var hash = hmac.ComputeHash(plaintextBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
