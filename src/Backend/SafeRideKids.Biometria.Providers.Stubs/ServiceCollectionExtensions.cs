using Microsoft.Extensions.DependencyInjection;

namespace SafeRideKids.Biometria.Providers.Stubs;

/// <summary>
/// Os stubs NÃO são registrados como IFaceVerificationProvider para não cair
/// no resolver — eles só existem como classes públicas para inspeção/documentação
/// e para serem instanciáveis em testes (que validam o "lança NotSupportedException").
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStubProvidersForDocumentation(this IServiceCollection services)
    {
        // Registrados apenas como concretes (sem interface) para uso em testes ou
        // health checks que queiram ler o ProviderId para listar na matriz.
        services.AddTransient<AzureFaceFaceVerificationProvider>();
        services.AddTransient<FaceTecFaceVerificationProvider>();
        services.AddTransient<SerproDatavalidFaceVerificationProvider>();
        return services;
    }
}
