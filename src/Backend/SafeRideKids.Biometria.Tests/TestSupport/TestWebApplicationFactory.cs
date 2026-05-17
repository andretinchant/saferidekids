using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeRideKids.Biometria.Api.Providers;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Security;

namespace SafeRideKids.Biometria.Tests.TestSupport;

/// <summary>
/// WebApplicationFactory que troca EF Core Npgsql por InMemory e substitui providers
/// reais por MockFaceProvider. Usado por testes de endpoint.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public List<MockFaceProvider> MockProviders { get; } = new()
    {
        new MockFaceProvider("aws-rekognition"),
        new MockFaceProvider("unico-idcloud")
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BIOMETRIA_PROVIDERS_ATIVOS"] = "aws-rekognition,unico-idcloud",
                ["BIOMETRIA_DEFAULT_PROVIDER"] = "aws-rekognition",
                ["TENANT_SALT_SECRET"] = "test-salt",
                ["BIOMETRIA_PG_ENCRYPTION_KEY_SECRET"] = "test-key",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove BiometriaDbContext registrado (Npgsql) e injeta InMemory.
            services.RemoveAll<DbContextOptions<BiometriaDbContext>>();
            services.RemoveAll<BiometriaDbContext>();
            services.AddDbContext<BiometriaDbContext>(opts => opts.UseInMemoryDatabase($"biometria_{Guid.NewGuid()}"));

            // Substitui tenant secrets por in-memory (sem Secrets Manager).
            services.RemoveAll<ITenantSecretsProvider>();
            services.AddSingleton<ITenantSecretsProvider>(_ => new InMemoryTenantSecretsProvider());

            // Substitui providers reais pelos mocks.
            services.RemoveAll<IFaceVerificationProvider>();
            foreach (var mock in MockProviders)
            {
                services.AddSingleton<IFaceVerificationProvider>(mock);
            }
            services.RemoveAll<IFaceVerificationProviderResolver>();
            services.AddScoped<IFaceVerificationProviderResolver, FaceVerificationProviderResolver>();
        });
    }
}
