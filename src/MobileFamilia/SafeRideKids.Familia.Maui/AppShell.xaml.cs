using SafeRideKids.Familia.Maui.Services;
using SafeRideKids.Familia.Maui.Views;

namespace SafeRideKids.Familia.Maui;

public partial class AppShell : Shell
{
    private readonly IAuthService _auth;

    public AppShell(IAuthService auth)
    {
        InitializeComponent();
        _auth = auth;

        // Rotas para navegação por push (não aparecem na flyout)
        Routing.RegisterRoute("addChild", typeof(AddChildPage));
        Routing.RegisterRoute("consent", typeof(ConsentPage));
        Routing.RegisterRoute("enrollment", typeof(EnrollmentPage));
        Routing.RegisterRoute("childDetail", typeof(ChildDetailPage));
    }

    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        // Gate de autenticação: redireciona para //login se não autenticado.
        if (args.Target?.Location is null) return;
        var target = args.Target.Location.OriginalString;
        if (target.StartsWith("//login")) return;

        var ok = await _auth.IsAuthenticatedAsync().ConfigureAwait(true);
        if (!ok)
        {
            args.Cancel();
            await Shell.Current.GoToAsync("//login").ConfigureAwait(true);
        }
    }
}
