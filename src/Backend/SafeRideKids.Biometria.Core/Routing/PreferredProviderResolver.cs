namespace SafeRideKids.Biometria.Core.Routing;

/// <summary>
/// Resolve override de provider preferido por família (consultando o repository).
/// Quebra a dependência do router em EF Core / DbContext (Core não conhece infra).
/// </summary>
public interface IPreferredProviderResolver
{
    /// <summary>
    /// Retorna o providerId preferido da família, ou null caso não haja override.
    /// </summary>
    string? GetPreferredProviderId(string tenantId, string familyId);
}
