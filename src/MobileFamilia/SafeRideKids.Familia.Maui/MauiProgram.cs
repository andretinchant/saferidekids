using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Refit;
using SafeRideKids.Familia.Maui.Services;
using SafeRideKids.Familia.Maui.ViewModels;
using SafeRideKids.Familia.Maui.Views;

namespace SafeRideKids.Familia.Maui;

public static class MauiProgram
{
    /// <summary>
    /// URL base do backend. POC: localhost em emulator Android usa 10.0.2.2.
    /// Em build final, sobrescrever via configuração ou hard-code conforme deploy.
    /// </summary>
    public const string DefaultBackendBaseUrl = "https://api.saferidekids.example";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();
        // Observação: para registrar fontes customizadas, adicione os .ttf em
        // Resources/Fonts e chame .ConfigureFonts(...) aqui. Mantemos o default
        // do MAUI para evitar dependência de assets não versionados na POC.

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Serviços de aplicação
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IConsentTextProvider, ConsentTextProvider>();
        builder.Services.AddSingleton<IChildrenStore, ChildrenStore>();
        builder.Services.AddSingleton<ICameraCaptureService, CameraCaptureService>();
        builder.Services.AddTransient<BearerTokenHandler>();

        // HTTP / Refit
        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            })
        };

        builder.Services
            .AddRefitClient<IBackendApi>(refitSettings)
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(DefaultBackendBaseUrl);
                c.Timeout = TimeSpan.FromSeconds(60); // multipart upload tolera 60s
            })
            .AddHttpMessageHandler<BearerTokenHandler>();

        // ViewModels (Transient para que cada navegação tenha estado limpo)
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<MyChildrenViewModel>();
        builder.Services.AddTransient<AddChildViewModel>();
        builder.Services.AddTransient<ConsentViewModel>();
        builder.Services.AddTransient<EnrollmentViewModel>();
        builder.Services.AddTransient<ChildDetailViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MyChildrenPage>();
        builder.Services.AddTransient<AddChildPage>();
        builder.Services.AddTransient<ConsentPage>();
        builder.Services.AddTransient<EnrollmentPage>();
        builder.Services.AddTransient<ChildDetailPage>();

        // Shell — único
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
