using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Core.Routing;

namespace SafeRideKids.Biometria.Api.Providers;

/// <summary>
/// Resolve uma instância de IFaceVerificationProvider pelo providerId.
/// Filtra pela lista BIOMETRIA_PROVIDERS_ATIVOS — providers registrados mas
/// não listados ficam disponíveis apenas via Resolve(id) explícito (útil para DSR-delete
/// de enrollments antigos quando provedor foi desativado).
/// </summary>
public interface IFaceVerificationProviderResolver
{
    /// <summary>Resolve provider concreto por id. Erro se não registrado.</summary>
    IFaceVerificationProvider Resolve(string providerId);

    /// <summary>Lista apenas providers ATIVOS (filtrados pelo env BIOMETRIA_PROVIDERS_ATIVOS).</summary>
    IReadOnlyList<IFaceVerificationProvider> All();
}

/// <inheritdoc />
public sealed class FaceVerificationProviderResolver : IFaceVerificationProviderResolver
{
    private readonly Dictionary<string, IFaceVerificationProvider> _byId;
    private readonly IReadOnlyList<IFaceVerificationProvider> _active;

    public FaceVerificationProviderResolver(
        IEnumerable<IFaceVerificationProvider> providers,
        IOptions<ProviderRoutingOptions> routingOptions)
    {
        var all = providers.ToList();
        _byId = all.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);

        var activeIds = routingOptions.Value.ActiveProviderIds;
        _active = all
            .Where(p => activeIds.Contains(p.ProviderId, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public IFaceVerificationProvider Resolve(string providerId) =>
        _byId.TryGetValue(providerId, out var p)
            ? p
            : throw new InvalidOperationException($"Provider biométrico não registrado: {providerId}");

    public IReadOnlyList<IFaceVerificationProvider> All() => _active;
}
