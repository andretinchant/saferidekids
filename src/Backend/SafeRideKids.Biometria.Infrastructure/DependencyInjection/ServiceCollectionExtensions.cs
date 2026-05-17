using System;
using Amazon.KeyManagementService;
using Amazon.S3;
using Amazon.SecretsManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRideKids.Biometria.Core.Audit;
using SafeRideKids.Biometria.Core.Fallback;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Notifications;
using SafeRideKids.Biometria.Core.Routing;
using SafeRideKids.Biometria.Core.Security;
using SafeRideKids.Biometria.Core.Storage;
using SafeRideKids.Biometria.Infrastructure.Audit;
using SafeRideKids.Biometria.Infrastructure.Fallback;
using SafeRideKids.Biometria.Infrastructure.Notifications;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Routing;
using SafeRideKids.Biometria.Infrastructure.Security;
using SafeRideKids.Biometria.Infrastructure.Storage;

namespace SafeRideKids.Biometria.Infrastructure.DependencyInjection;

/// <summary>
/// DI registrations da camada de Infrastructure. Chamado pela API.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBiometriaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ----- Options ----------------------------------------------------
        services.Configure<ProviderRoutingOptions>(opts =>
        {
            var raw = configuration["BIOMETRIA_PROVIDERS_ATIVOS"] ?? "aws-rekognition,unico-idcloud";
            opts.ActiveProviderIds = new System.Collections.Generic.List<string>(
                raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var def = configuration["BIOMETRIA_DEFAULT_PROVIDER"];
            if (!string.IsNullOrWhiteSpace(def)) opts.DefaultProviderId = def;
        });

        services.Configure<TenantSecretsOptions>(opts =>
        {
            opts.HmacSaltSecretId = configuration["TENANT_SALT_SECRET"] ?? string.Empty;
            opts.PgEncryptionKeySecretId = configuration["BIOMETRIA_PG_ENCRYPTION_KEY_SECRET"] ?? string.Empty;
        });

        services.Configure<S3TemplateStorageOptions>(opts =>
        {
            opts.BucketName = configuration["BIOMETRIA_S3_TEMPLATE_BUCKET"] ?? string.Empty;
            opts.KmsKeyId = configuration["BIOMETRIA_KMS_KEY_ID"] ?? string.Empty;
            opts.SignedDocumentsBucketName = configuration["BIOMETRIA_S3_CONSENT_BUCKET"] ?? string.Empty;
        });

        // ----- EF Core / Npgsql -------------------------------------------
        var connString = configuration["BIOMETRIA_PG_CONNSTRING"]
                         ?? "Host=localhost;Database=saferidekids_biometria;Username=postgres;Password=postgres";
        services.AddDbContext<BiometriaDbContext>(opts => opts.UseNpgsql(connString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BiometriaDbContext.SchemaName);
        }));

        // ----- AWS SDK clients (singletons, descobrem credenciais default) -
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
        services.AddSingleton<IAmazonKeyManagementService>(_ => new AmazonKeyManagementServiceClient());
        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());

        // ----- Core abstractions → infra impls ----------------------------
        services.AddSingleton<ITenantSecretsProvider, AwsSecretsManagerTenantSecretsProvider>();
        services.AddScoped<IHmacHasher, HmacHasher>();
        services.AddScoped<ITemplateStorage, S3TemplateStorage>();
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();
        services.AddScoped<IFallbackPinService, FallbackPinService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // ----- Routing ----------------------------------------------------
        services.AddScoped<IPreferredProviderResolver, PreferredProviderResolverEf>();
        services.AddScoped<IProviderRouter, HashBasedProviderRouter>();

        return services;
    }

    /// <summary>
    /// Variante para testes/locais com tenant secrets in-memory (sem AWS).
    /// </summary>
    public static IServiceCollection AddBiometriaInfrastructureInMemorySecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddBiometriaInfrastructure(services, configuration);
        services.AddSingleton<ITenantSecretsProvider>(_ => new InMemoryTenantSecretsProvider());
        return services;
    }
}
