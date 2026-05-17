using SafeRideKids.Biometria.Core.Multitenancy;

namespace SafeRideKids.Biometria.Api.Multitenancy;

/// <summary>Implementação scoped do ITenantContext populada pelo middleware.</summary>
public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public string ActorType { get; private set; } = "system";
    public bool IsAuthenticated { get; private set; }

    public void Populate(string tenantId, string actorId, string actorType)
    {
        TenantId = tenantId;
        ActorId = actorId;
        ActorType = actorType;
        IsAuthenticated = !string.IsNullOrWhiteSpace(actorId);
    }
}
