using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui;

// Configura todas as rotas usadas nas navegacoes Shell.Current.GoToAsync(...).
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Rotas internas (paginas filhas, navegadas a partir das principais).
        Routing.RegisterRoute(nameof(TodayRoutePage), typeof(TodayRoutePage));
        Routing.RegisterRoute(nameof(CheckInPage), typeof(CheckInPage));
        Routing.RegisterRoute(nameof(LivenessCapturePage), typeof(LivenessCapturePage));
        Routing.RegisterRoute(nameof(FallbackPinPage), typeof(FallbackPinPage));
        Routing.RegisterRoute(nameof(FallbackManualPage), typeof(FallbackManualPage));
        Routing.RegisterRoute(nameof(CheckInHistoryPage), typeof(CheckInHistoryPage));
    }
}
