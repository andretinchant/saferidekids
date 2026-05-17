using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

public sealed partial class TodayRouteViewModel : BaseViewModel
{
    private readonly IBackendApi _api;
    private readonly IConnectivityWatcher _connectivity;
    private readonly IOfflineQueueService _offlineQueue;
    private readonly IAuthService _auth;

    public TodayRouteViewModel(
        IBackendApi api,
        IConnectivityWatcher connectivity,
        IOfflineQueueService offlineQueue,
        IAuthService auth)
    {
        _api = api;
        _connectivity = connectivity;
        _offlineQueue = offlineQueue;
        _auth = auth;
    }

    [ObservableProperty]
    private string driverName = string.Empty;

    [ObservableProperty]
    private string routeId = string.Empty;

    [ObservableProperty]
    private DateOnly scheduledDate;

    [ObservableProperty]
    private bool isOnline = true;

    public ObservableCollection<RouteStopDto> Stops { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        IsBusy = true;
        try
        {
            DriverName = _auth.CurrentDisplayName ?? string.Empty;
            IsOnline = _connectivity.IsOnline;

            var resp = await _api.GetTodayRouteAsync(CancellationToken.None);
            RouteId = resp.RouteId;
            ScheduledDate = resp.ScheduledDate;
            Stops.Clear();
            foreach (var s in resp.Stops)
            {
                Stops.Add(s);
            }

            // Aproveita para tentar sincronizar pendencias offline.
            if (_connectivity.IsOnline)
            {
                _ = Task.Run(() => _offlineQueue.SyncPendingAsync(_api, CancellationToken.None));
            }
        }
        catch (HttpRequestException)
        {
            // Sem rede: o app ainda funciona em fallback manual offline.
            IsOnline = false;
            ErrorMessage = "Sem conexao. Voce pode iniciar embarques em modo manual offline.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro ao carregar rota: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectStopAsync(RouteStopDto? stop)
    {
        if (stop is null) return;
        // Passa o stop por shell navigation parameter; CheckInPage le via QueryProperty.
        var navParams = new Dictionary<string, object>
        {
            ["StopId"] = stop.StopId,
            ["ChildId"] = stop.ChildId,
            ["DisplayName"] = stop.DisplayName,
            ["AddressLabel"] = stop.AddressLabel,
            ["BoardingMode"] = stop.BoardingMode
        };
        await Shell.Current.GoToAsync(nameof(CheckInPage), navParams);
    }

    [RelayCommand]
    private async Task ShowHistoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(CheckInHistoryPage));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}
