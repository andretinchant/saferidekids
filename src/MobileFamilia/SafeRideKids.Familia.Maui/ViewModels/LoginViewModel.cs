using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// ViewModel da tela de login. Na POC o backend de auth é placeholder;
/// se a chamada a /oauth/token falhar, gravamos um token sintético
/// (mock-token) apenas para permitir navegação e desenvolvimento offline.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IBackendApi _api;
    private readonly IAuthService _auth;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string tenantId = "tenant-poc";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;

    public LoginViewModel(IBackendApi api, IAuthService auth, ILogger<LoginViewModel> logger)
    {
        _api = api;
        _auth = auth;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            try
            {
                var request = new LoginRequest(Email.Trim(), Password, TenantId.Trim());
                var response = await _api.LoginAsync(request, cancellationToken).ConfigureAwait(false);
                await _auth.SetTokensAsync(response.AccessToken, response.RefreshToken, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // POC: backend pode não estar disponível — token mock para fluxo de dev.
                _logger.LogWarning(ex, "Login real falhou — usando token mock (apenas POC).");
                await _auth.SetTokensAsync(
                    accessToken: $"mock-token-{Guid.NewGuid():N}",
                    refreshToken: null,
                    cancellationToken).ConfigureAwait(false);
            }

            await Shell.Current.GoToAsync("//children").ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() => !IsBusy
                                && !string.IsNullOrWhiteSpace(Email)
                                && !string.IsNullOrWhiteSpace(Password);

    partial void OnEmailChanged(string value) => SignInCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => SignInCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => SignInCommand.NotifyCanExecuteChanged();
}
