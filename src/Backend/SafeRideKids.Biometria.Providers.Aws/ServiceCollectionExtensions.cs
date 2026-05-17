using System;
using Amazon.Rekognition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Providers.Aws;

/// <summary>DI registrations do provider AWS Rekognition.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsRekognitionProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AwsRekognitionOptions>(_ => { /* defaults */ });

        services.AddSingleton<IAmazonRekognition>(_ => new AmazonRekognitionClient());

        services.AddScoped<IFaceVerificationProvider, AwsRekognitionFaceVerificationProvider>();

        return services;
    }
}
