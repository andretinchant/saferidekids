using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Refit;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.ViewModels;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui;

// Bootstrap do MAUI: registra DI, HttpClient (Refit) e fábrica dos ViewModels.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Backend HTTP via Refit. Em produção, a URL viria de config / Secrets Manager.
        // Para a POC, ler de ambiente ou usar default localhost-friendly p/ emulador.
        var backendBaseUrl = Environment.GetEnvironmentVariable("SAFERIDE_BACKEND_URL")
                             ?? "https://api.saferidekids.poc.local";

        builder.Services
            .AddRefitClient<IBackendApi>(_ => new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(BackendJsonOptions.Default)
            })
            .ConfigureHttpClient((sp, http) =>
            {
                http.BaseAddress = new Uri(backendBaseUrl);
                http.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler(sp => new AuthHeaderHandler(sp.GetRequiredService<IAuthService>()));

        // Serviços de aplicação.
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IConnectivityWatcher, ConnectivityWatcher>();
        builder.Services.AddSingleton<IOfflineQueueService, OfflineQueueService>();
        builder.Services.AddSingleton<ILivenessSdkOrchestrator, LivenessSdkOrchestrator>();
        builder.Services.AddSingleton<IGeolocationProvider, GeolocationProvider>();
        builder.Services.AddSingleton<IHapticsService, HapticsService>();

        // ViewModels: transient para garantir estado limpo a cada navegação.
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<TodayRouteViewModel>();
        builder.Services.AddTransient<CheckInViewModel>();
        builder.Services.AddTransient<LivenessCaptureViewModel>();
        builder.Services.AddTransient<FallbackPinViewModel>();
        builder.Services.AddTransient<FallbackManualViewModel>();
        builder.Services.AddTransient<CheckInHistoryViewModel>();

        // Pages com DI explicita.
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<TodayRoutePage>();
        builder.Services.AddTransient<CheckInPage>();
        builder.Services.AddTransient<LivenessCapturePage>();
        builder.Services.AddTransient<FallbackPinPage>();
        builder.Services.AddTransient<FallbackManualPage>();
        builder.Services.AddTransient<CheckInHistoryPage>();

        return builder.Build();
    }
}
