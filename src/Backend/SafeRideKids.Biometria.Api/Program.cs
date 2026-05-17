using System;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SafeRideKids.Biometria.Api.Endpoints;
using SafeRideKids.Biometria.Api.ExceptionHandling;
using SafeRideKids.Biometria.Api.Multitenancy;
using SafeRideKids.Biometria.Api.Providers;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Infrastructure.DependencyInjection;
using SafeRideKids.Biometria.Providers.Aws;
using SafeRideKids.Biometria.Providers.Stubs;
using SafeRideKids.Biometria.Providers.Unico;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", optional: true);

// Lambda hosting (ASP.NET minimal API). Comportamento idêntico local quando rodando via `dotnet run`.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- Multi-tenant -----------------------------------------------------------
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// --- AuthN/AuthZ (Cognito JWT) ---------------------------------------------
var cognitoPool = builder.Configuration["COGNITO_USER_POOL_ID"];
// COGNITO_CLIENT_ID lido apenas para validação no futuro (token introspection); por enquanto não bloqueia.
_ = builder.Configuration["COGNITO_CLIENT_ID"];
var awsRegion = builder.Configuration["AWS_REGION"] ?? "us-east-1";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = $"https://cognito-idp.{awsRegion}.amazonaws.com/{cognitoPool}";
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(cognitoPool),
            ValidateAudience = false, // Cognito Access Token não usa 'aud'; client_id valida via custom.
            ValidateLifetime = true,
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(cognitoPool),
            ValidIssuer = $"https://cognito-idp.{awsRegion}.amazonaws.com/{cognitoPool}"
        };
        // Em ambiente de teste sem Cognito real, JwtBearer não falha em requests sem token
        // (auth não é exigida globalmente — middleware de tenant tem fallback X-Dev-Tenant).
        opts.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

// --- Infra & Providers -----------------------------------------------------
builder.Services.AddBiometriaInfrastructure(builder.Configuration);
builder.Services.AddAwsRekognitionProvider(builder.Configuration);
builder.Services.AddUnicoProvider(builder.Configuration);
builder.Services.AddStubProvidersForDocumentation();

// Resolver de providers ativos. Scoped porque os providers concretos são scoped/transient
// (precisam acessar IAmazonRekognition singleton mas o resolver vive por request).
builder.Services.AddScoped<IFaceVerificationProviderResolver, FaceVerificationProviderResolver>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(Enum.TryParse<LogLevel>(builder.Configuration["LOG_LEVEL"], true, out var lvl) ? lvl : LogLevel.Information);

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Middleware de tenant DEPOIS do auth para popular ActorId do claim.
app.UseMiddleware<TenantResolutionMiddleware>();

// Health endpoint trivial.
app.MapGet("/health", () => Results.Ok(new { status = "ok", ts = DateTimeOffset.UtcNow }))
   .WithTags("health");

// Endpoints de negócio.
app.MapFamilyEndpoints();
app.MapMotoristaEndpoints();
app.MapDashboardEndpoints();

app.Run();

/// <summary>Marker para Program test fixture (WebApplicationFactory).</summary>
public partial class Program { }
