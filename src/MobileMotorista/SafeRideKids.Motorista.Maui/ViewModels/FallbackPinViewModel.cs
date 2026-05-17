using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

[QueryProperty(nameof(CheckInId), nameof(CheckInId))]
[QueryProperty(nameof(ChildDisplayName), nameof(ChildDisplayName))]
public sealed partial class FallbackPinViewModel : BaseViewModel
{
    private readonly IBackendApi _api;
    private readonly IGeolocationProvider _geo;
    private readonly IHapticsService _haptics;

    public FallbackPinViewModel(IBackendApi api, IGeolocationProvider geo, IHapticsService haptics)
    {
        _api = api;
        _geo = geo;
        _haptics = haptics;
    }

    [ObservableProperty]
    private string checkInId = string.Empty;

    [ObservableProperty]
    private string childDisplayName = string.Empty;

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private int attemptsLeft = 3;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ClearMessages();

        if (Pin.Length != 6 || !Pin.All(char.IsDigit))
        {
            ErrorMessage = "O PIN deve ter 6 digitos numericos.";
            return;
        }

        IsBusy = true;
        try
        {
            var resp = await _api.FallbackPinAsync(
                new FallbackPinRequest(CheckInId, Pin),
                CancellationToken.None);

            if (!resp.Success)
            {
                _haptics.Warning();
                AttemptsLeft = resp.AttemptsLeft ?? Math.Max(0, AttemptsLeft - 1);
                ErrorMessage = resp.ErrorMessage ?? $"PIN invalido. Tentativas restantes: {AttemptsLeft}";

                if (AttemptsLeft <= 0)
                {
                    InfoMessage = "PIN bloqueado. Use confirmacao manual.";
                    var nav = new Dictionary<string, object>
                    {
                        ["CheckInId"] = CheckInId,
                        ["StopId"] = string.Empty,
                        ["ChildId"] = string.Empty,
                        ["ChildDisplayName"] = ChildDisplayName,
                        ["AddressLabel"] = string.Empty,
                        ["IsOffline"] = false
                    };
                    await Shell.Current.GoToAsync(nameof(FallbackManualPage), nav);
                }
                return;
            }

            _haptics.Success();
            InfoMessage = "PIN validado. Confirmando embarque...";

            var geo = await _geo.TryGetCurrentLocationAsync(CancellationToken.None);
            await _api.ConfirmCheckInAsync(
                new CheckInConfirmRequest(CheckInId, geo?.Lat, geo?.Lng),
                CancellationToken.None);

            await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
        }
        catch (HttpRequestException)
        {
            // Sem rede no fallback PIN: PIN exige backend, nao da pra validar offline.
            // Caminho previsto pelo prompt: cair em fallback manual offline.
            InfoMessage = "Sem conexao para validar PIN. Use confirmacao manual offline.";
            var nav = new Dictionary<string, object>
            {
                ["CheckInId"] = CheckInId,
                ["StopId"] = string.Empty,
                ["ChildId"] = string.Empty,
                ["ChildDisplayName"] = ChildDisplayName,
                ["AddressLabel"] = string.Empty,
                ["IsOffline"] = true
            };
            await Shell.Current.GoToAsync(nameof(FallbackManualPage), nav);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro ao validar PIN: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
