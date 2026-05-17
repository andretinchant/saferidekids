using SafeRideKids.Motorista.Maui.Services;

namespace SafeRideKids.Motorista.Maui;

// Bootstrapping da Application. Carrega o Shell e configura tema.
public partial class App : Application
{
    public App(IConnectivityWatcher connectivity)
    {
        InitializeComponent();

        // Respeita preferencia do sistema (claro/escuro). Acessibilidade.
        UserAppTheme = AppTheme.Unspecified;

        // Inicia monitoramento de conectividade — fila offline depende dele.
        connectivity.Start();

        MainPage = new AppShell();
    }
}
