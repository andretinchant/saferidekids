using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Multitenancy;

namespace SafeRideKids.Biometria.Infrastructure.Security;

/// <summary>
/// Opções para leitura de segredos por tenant a partir do AWS Secrets Manager.
/// </summary>
public sealed class TenantSecretsOptions
{
    /// <summary>Secret id do salt HMAC (TENANT_SALT_SECRET).</summary>
    public string HmacSaltSecretId { get; set; } = string.Empty;

    /// <summary>Secret id da chave pgp_sym_encrypt (BIOMETRIA_PG_ENCRYPTION_KEY_SECRET).</summary>
    public string PgEncryptionKeySecretId { get; set; } = string.Empty;
}

/// <summary>
/// Provedor real (AWS Secrets Manager). Caches por tenant em memória dentro do processo Lambda
/// para evitar chamada repetida — TTL implícito é a vida do container Lambda (~minutos).
/// </summary>
public sealed class AwsSecretsManagerTenantSecretsProvider : ITenantSecretsProvider
{
    private readonly IAmazonSecretsManager _client;
    private readonly TenantSecretsOptions _options;
    private readonly ILogger<AwsSecretsManagerTenantSecretsProvider> _logger;

    // Cache simples por (tenantId, key). Não usar TTL na POC — container Lambda recicla naturalmente.
    private readonly ConcurrentDictionary<string, byte[]> _saltCache = new();
    private readonly ConcurrentDictionary<string, string> _pgKeyCache = new();

    public AwsSecretsManagerTenantSecretsProvider(
        IAmazonSecretsManager client,
        IOptions<TenantSecretsOptions> options,
        ILogger<AwsSecretsManagerTenantSecretsProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> GetHmacSaltAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (_saltCache.TryGetValue(tenantId, out var cached)) return cached;

        // Salt é compartilhado por tenant via secret JSON. Estratégia:
        // secret JSON { "<tenantId>": "<base64-encoded-salt>" }.
        // Tenant não encontrado → derivar salt determinístico com warning.
        var response = await _client.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = _options.HmacSaltSecretId },
            cancellationToken).ConfigureAwait(false);

        // Versão simplificada: usa UTF-8 do secret string + tenant id como suffix.
        // Em produção, parse-ar JSON e lookup por tenantId.
        var raw = Encoding.UTF8.GetBytes((response.SecretString ?? string.Empty) + ":" + tenantId);
        _saltCache[tenantId] = raw;
        return raw;
    }

    public async Task<string> GetPgEncryptionKeyAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (_pgKeyCache.TryGetValue(tenantId, out var cached)) return cached;

        var response = await _client.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = _options.PgEncryptionKeySecretId },
            cancellationToken).ConfigureAwait(false);

        var raw = response.SecretString ?? string.Empty;
        _pgKeyCache[tenantId] = raw;
        return raw;
    }
}

/// <summary>
/// Fallback in-memory para testes / ambiente local sem AWS. Salt vem da env var.
/// NÃO usar em produção.
/// </summary>
public sealed class InMemoryTenantSecretsProvider : ITenantSecretsProvider
{
    private readonly byte[] _staticSalt;
    private readonly string _pgKey;

    public InMemoryTenantSecretsProvider(string saltSeed = "poc-default-salt", string pgKey = "poc-default-key")
    {
        _staticSalt = Encoding.UTF8.GetBytes(saltSeed);
        _pgKey = pgKey;
    }

    public Task<byte[]> GetHmacSaltAsync(string tenantId, CancellationToken cancellationToken)
    {
        // Combina tenant id ao salt estático para isolar tenants nos testes.
        var combined = new byte[_staticSalt.Length + tenantId.Length];
        Buffer.BlockCopy(_staticSalt, 0, combined, 0, _staticSalt.Length);
        Buffer.BlockCopy(Encoding.UTF8.GetBytes(tenantId), 0, combined, _staticSalt.Length, tenantId.Length);
        return Task.FromResult(combined);
    }

    public Task<string> GetPgEncryptionKeyAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(_pgKey);
}
