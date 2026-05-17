using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Unico;

/// <summary>DI registrations do provider Unico IDCloud.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUnicoProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<UnicoOptions>(opts =>
        {
            var baseUrl = configuration["UNICO_API_BASE_URL"];
            var apiKey = configuration["UNICO_API_KEY"];     // POC: pode vir direto de env; produção lê do Secrets Manager.
            if (!string.IsNullOrWhiteSpace(baseUrl)) opts.BaseUrl = baseUrl;
            if (!string.IsNullOrWhiteSpace(apiKey)) opts.ApiKey = apiKey;
        });

        services.AddHttpClient<UnicoFaceVerificationProvider>();
        // Registra como IFaceVerificationProvider (transient porque o HttpClientFactory já gerencia handler lifetime).
        services.AddTransient<IFaceVerificationProvider>(sp => sp.GetRequiredService<UnicoFaceVerificationProvider>());

        return services;
    }
}
