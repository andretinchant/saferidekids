using System;
using Microsoft.EntityFrameworkCore;
using SafeRideKids.Biometria.Core.Routing;
using SafeRideKids.Biometria.Infrastructure.Persistence;

namespace SafeRideKids.Biometria.Infrastructure.Routing;

/// <summary>
/// Resolver síncrono consultando o DbContext. Usa AsNoTracking e leitura simples.
/// Em produção convém adicionar cache em memória scopado por request.
/// </summary>
public sealed class PreferredProviderResolverEf : IPreferredProviderResolver
{
    private readonly BiometriaDbContext _db;

    public PreferredProviderResolverEf(BiometriaDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public string? GetPreferredProviderId(string tenantId, string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId)) return null;
        if (!Guid.TryParse(familyId, out var familyGuid)) return null;

        // Lookup sincrono — EF Core 8 suporta. Caller já está em context onde
        // a chamada bloqueia brevemente; throughput por request não é gargalo na POC.
        return _db.Families
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.Id == familyGuid)
            .Select(f => f.PreferredProviderId)
            .FirstOrDefault();
    }
}
