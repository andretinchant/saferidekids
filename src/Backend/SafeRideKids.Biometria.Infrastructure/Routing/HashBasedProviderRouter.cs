using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Routing;

namespace SafeRideKids.Biometria.Infrastructure.Routing;

/// <summary>
/// Router determinístico baseado em SHA-256 de (childId|YYYYMMDD) mod N
/// conforme CONTRACTS.md Seção 4. Override por family.preferred_provider_id
/// quando definido (consultado via IPreferredProviderResolver).
///
/// Propriedades:
///   - Determinístico: mesmo input → sempre mesmo output (estabilidade UX).
///   - Balanceado: SHA-256 distribui ~uniformemente entre as N opções.
///   - Stateless: nenhuma dependência de DB para o caminho default.
/// </summary>
public sealed class HashBasedProviderRouter : IProviderRouter
{
    private readonly IPreferredProviderResolver _preferredResolver;
    private readonly ProviderRoutingOptions _options;
    private readonly ILogger<HashBasedProviderRouter> _logger;

    public HashBasedProviderRouter(
        IPreferredProviderResolver preferredResolver,
        IOptions<ProviderRoutingOptions> options,
        ILogger<HashBasedProviderRouter> logger)
    {
        _preferredResolver = preferredResolver ?? throw new ArgumentNullException(nameof(preferredResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ResolveProviderId(string tenantId, string familyId, string childId, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId vazio.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId vazio.", nameof(childId));

        // 1) Override por família (consulta DB cacheada — caller pode usar memory cache).
        var preferred = _preferredResolver.GetPreferredProviderId(tenantId, familyId);
        if (!string.IsNullOrWhiteSpace(preferred) && _options.ActiveProviderIds.Contains(preferred))
        {
            return preferred;
        }

        var active = (IReadOnlyList<string>)_options.ActiveProviderIds;
        if (active.Count == 0)
        {
            _logger.LogWarning("BIOMETRIA_PROVIDERS_ATIVOS vazio; usando DefaultProviderId={DefaultProviderId}", _options.DefaultProviderId);
            return _options.DefaultProviderId;
        }

        // 2) Hash determinístico: SHA-256 de "childId|YYYYMMDD" em UTC,
        //    pegamos os 8 primeiros bytes como ulong → mod N.
        var dateBucket = utcNow.UtcDateTime.ToString("yyyyMMdd");
        var key = $"{childId}|{dateBucket}";
        var bucket = BucketOf(key, active.Count);

        return active[bucket];
    }

    private static int BucketOf(string key, int modulus)
    {
        Span<byte> hash = stackalloc byte[32];
        var bytes = Encoding.UTF8.GetBytes(key);
        SHA256.HashData(bytes, hash);

        // Pegamos 8 bytes (64 bits) → ulong não-assinado → mod N positivo garantido.
        ulong head = 0;
        for (int i = 0; i < 8; i++)
        {
            head = (head << 8) | hash[i];
        }

        return (int)(head % (ulong)modulus);
    }
}

/// <summary>
/// Implementação default que sempre retorna null (sem override).
/// Wire-up real consulta o DbContext — definido na Infrastructure (PreferredProviderResolverEf).
/// </summary>
public sealed class NoopPreferredProviderResolver : IPreferredProviderResolver
{
    public string? GetPreferredProviderId(string tenantId, string familyId) => null;
}
