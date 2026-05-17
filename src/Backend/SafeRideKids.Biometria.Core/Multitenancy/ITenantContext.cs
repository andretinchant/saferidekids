namespace SafeRideKids.Biometria.Core.Multitenancy;

/// <summary>
/// Contexto multi-tenant injetado pelo middleware da API a partir do claim
/// "custom:tenantId" do JWT do Cognito. Scope = request.
/// </summary>
public interface ITenantContext
{
    /// <summary>Tenant resolvido para o request corrente.</summary>
    string TenantId { get; }

    /// <summary>Identidade do ator autenticado (id Cognito sub). Pode vir vazia em requests anônimos.</summary>
    string ActorId { get; }

    /// <summary>Tipo do ator: 'family' | 'motorista' | 'admin' | 'system'.</summary>
    string ActorType { get; }

    /// <summary>Indica se o contexto já foi populado (middleware rodou).</summary>
    bool IsAuthenticated { get; }
}
