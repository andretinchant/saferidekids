using System;

namespace SafeRideKids.Biometria.Core.Routing;

/// <summary>
/// Router server-side (Approach C) que decide qual provedor biométrico usar
/// para determinada criança em determinado check-in.
/// Estratégia padrão: hash determinístico (childId + UTC date) modulo n
/// provedores, com override por family.preferred_provider_id.
/// </summary>
public interface IProviderRouter
{
    /// <summary>
    /// Decide qual provedor usar para este check-in. Estratégia padrão é hash determinístico
    /// (childId + UTC date) modulo 2 → 50/50 entre AWS e Unico, com a mesma criança
    /// sempre indo no mesmo provedor no mesmo dia (estabilidade UX e telemetria).
    /// Override por family.preferred_provider_id quando definido.
    /// </summary>
    string ResolveProviderId(string tenantId, string familyId, string childId, DateTimeOffset utcNow);
}
