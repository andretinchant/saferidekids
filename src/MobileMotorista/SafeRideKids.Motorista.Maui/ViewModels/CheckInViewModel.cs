using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

// Detalhe da parada com o botao grande "Iniciar check-in".
// Recebe parametros via Shell QueryProperty (setados pelo TodayRouteViewModel).
[QueryProperty(nameof(StopId), nameof(StopId))]
[QueryProperty(nameof(ChildId), nameof(ChildId))]
[QueryProperty(nameof(DisplayName), nameof(DisplayName))]
[QueryProperty(nameof(AddressLabel), nameof(AddressLabel))]
[QueryProperty(nameof(BoardingMode), nameof(BoardingMode))]
public sealed partial class CheckInViewModel : BaseViewModel
{
    private readonly IBackendApi _api;
    private readonly IConnectivityWatcher _connectivity;
    private readonly IOfflineQueueService _offlineQueue;
    private readonly IHapticsService _haptics;

    public CheckInViewModel(
        IBackendApi api,
        IConnectivityWatcher connectivity,
        IOfflineQueueService offlineQueue,
        IHapticsService haptics)
    {
        _api = api;
        _connectivity = connectivity;
        _offlineQueue = offlineQueue;
        _haptics = haptics;
    }

    [ObservableProperty]
    private string stopId = string.Empty;

    [ObservableProperty]
    private string childId = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string addressLabel = string.Empty;

    [ObservableProperty]
    private string boardingMode = "facial";

    [ObservableProperty]
    private CheckInState currentState = CheckInState.Idle;

    [RelayCommand]
    private async Task StartCheckInAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        _haptics.Light();
        IsBusy = true;
        CurrentState = CheckInState.StartingCheckIn;

        try
        {
            if (!_connectivity.IsOnline)
            {
                // Offline: o backend nao consegue abrir sessao liveness, vamos direto p/ fallback manual.
                InfoMessage = "Sem conexao — iniciando fallback manual offline.";
                await NavigateToFallbackManualAsync(checkInId: "offline-" + Guid.NewGuid().ToString("N")[..12]);
                return;
            }

            var resp = await _api.StartCheckInAsync(
                new CheckInStartRequest(StopId, ChildId),
                CancellationToken.None);

            CurrentState = CheckInState.CapturingLiveness;
            // Encaminha para a tela de captura passando dados da sessao.
            var nav = new Dictionary<string, object>
            {
                ["CheckInId"] = resp.CheckInId,
                ["ProviderId"] = resp.ProviderId,
                ["SessionId"] = resp.SessionInfo.SessionId,
                ["ChildDisplayName"] = DisplayName,
                ["AddressLabel"] = AddressLabel
            };
            await Shell.Current.GoToAsync(nameof(LivenessCapturePage), nav);
        }
        catch (HttpRequestException)
        {
            // Erro de rede -> fallback manual offline imediato.
            CurrentState = CheckInState.NetworkError;
            await NavigateToFallbackManualAsync(checkInId: "offline-" + Guid.NewGuid().ToString("N")[..12]);
        }
        catch (Exception ex)
        {
            CurrentState = CheckInState.Idle;
            ErrorMessage = "Erro ao iniciar check-in: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NavigateToFallbackManualAsync(string checkInId)
    {
        var nav = new Dictionary<string, object>
        {
            ["CheckInId"] = checkInId,
            ["StopId"] = StopId,
            ["ChildId"] = ChildId,
            ["ChildDisplayName"] = DisplayName,
            ["AddressLabel"] = AddressLabel,
            ["IsOffline"] = !_connectivity.IsOnline
        };
        await Shell.Current.GoToAsync(nameof(FallbackManualPage), nav);
    }
}
