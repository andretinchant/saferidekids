using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

public sealed partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly IHapticsService _haptics;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string tenantId = "tenant-poc";

    public LoginViewModel(IAuthService auth, IHapticsService haptics)
    {
        _auth = auth;
        _haptics = haptics;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        ClearMessages();

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Informe email e senha.";
            return;
        }

        try
        {
            IsBusy = true;
            // Mock POC. Substituir por chamada real ao Cognito (CONTRACTS Secao 13).
            await _auth.LoginMockAsync(Email.Trim(), Password, TenantId.Trim());
            _haptics.Success();
            // Caminho absoluto: limpa a pilha e vai para TodayRoute empilhada sob LoginPage.
            // MAUI Shell aceita formato //Root/Sub mesmo com sub registrada via RegisterRoute.
            await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Falha no login: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
