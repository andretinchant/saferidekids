using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

[QueryProperty(nameof(CheckInId), nameof(CheckInId))]
[QueryProperty(nameof(StopId), nameof(StopId))]
[QueryProperty(nameof(ChildId), nameof(ChildId))]
[QueryProperty(nameof(ChildDisplayName), nameof(ChildDisplayName))]
[QueryProperty(nameof(AddressLabel), nameof(AddressLabel))]
[QueryProperty(nameof(IsOffline), nameof(IsOffline))]
public sealed partial class FallbackManualViewModel : BaseViewModel
{
    private readonly IBackendApi _api;
    private readonly IOfflineQueueService _offlineQueue;
    private readonly IGeolocationProvider _geo;
    private readonly IConnectivityWatcher _connectivity;
    private readonly IHapticsService _haptics;

    public FallbackManualViewModel(
        IBackendApi api,
        IOfflineQueueService offlineQueue,
        IGeolocationProvider geo,
        IConnectivityWatcher connectivity,
        IHapticsService haptics)
    {
        _api = api;
        _offlineQueue = offlineQueue;
        _geo = geo;
        _connectivity = connectivity;
        _haptics = haptics;
    }

    [ObservableProperty]
    private string checkInId = string.Empty;

    [ObservableProperty]
    private string stopId = string.Empty;

    [ObservableProperty]
    private string childId = string.Empty;

    [ObservableProperty]
    private string childDisplayName = string.Empty;

    [ObservableProperty]
    private string addressLabel = string.Empty;

    [ObservableProperty]
    private bool isOffline;

    [ObservableProperty]
    private string justification = string.Empty;

    [ObservableProperty]
    private string? photoBase64;

    [ObservableProperty]
    private bool hasPhoto;

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        ClearMessages();
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                ErrorMessage = "Camera nao disponivel neste device.";
                return;
            }

            // Captura em memoria e converte para base64 imediatamente.
            // Importante: nao salvamos no disco. CONTRACTS Secao 8: imagens raw in-memory.
            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Foto de auditoria do embarque"
            });
            if (photo is null) return;

            using var stream = await photo.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            PhotoBase64 = Convert.ToBase64String(ms.ToArray());
            HasPhoto = true;
            InfoMessage = "Foto anexada ao registro de auditoria.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro ao capturar foto: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ClearPhoto()
    {
        PhotoBase64 = null;
        HasPhoto = false;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ClearMessages();

        if (string.IsNullOrWhiteSpace(Justification) || Justification.Trim().Length < 10)
        {
            ErrorMessage = "Descreva a justificativa (minimo 10 caracteres).";
            return;
        }

        IsBusy = true;
        try
        {
            var geo = await _geo.TryGetCurrentLocationAsync(CancellationToken.None);

            // Cenario offline: enfileira em SQLite e finaliza UX.
            // NAO armazenamos PhotoBase64 no SQLite (LGPD: sem imagem em disco).
            if (IsOffline || !_connectivity.IsOnline)
            {
                await _offlineQueue.EnqueueManualFallbackAsync(new OfflineCheckInRecord
                {
                    CheckInId = CheckInId,
                    RouteStopId = StopId,
                    ChildId = ChildId,
                    Justification = Justification.Trim(),
                    GeoLat = geo?.Lat,
                    GeoLng = geo?.Lng
                });

                _haptics.Success();
                InfoMessage = "Embarque registrado em modo offline. Sera enviado ao servidor automaticamente.";
                await Task.Delay(800);
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
                return;
            }

            // Online: envia direto. Foto vai por HTTP, sem cache local.
            await _api.FallbackManualAsync(
                new FallbackManualRequest(
                    CheckInId: CheckInId,
                    Justification: Justification.Trim(),
                    PhotoBase64: PhotoBase64,
                    OfflineSynced: false),
                CancellationToken.None);

            // Confirma com geolocalizacao.
            await _api.ConfirmCheckInAsync(
                new CheckInConfirmRequest(CheckInId, geo?.Lat, geo?.Lng),
                CancellationToken.None);

            _haptics.Success();
            InfoMessage = "Embarque manual registrado com sucesso.";
            await Task.Delay(800);
            await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
        }
        catch (HttpRequestException)
        {
            // Perdeu rede no meio do envio — enfileira como fallback offline.
            try
            {
                await _offlineQueue.EnqueueManualFallbackAsync(new OfflineCheckInRecord
                {
                    CheckInId = CheckInId,
                    RouteStopId = StopId,
                    ChildId = ChildId,
                    Justification = Justification.Trim(),
                });
                InfoMessage = "Sem conexao no envio. Salvo localmente para sincronizar depois.";
                await Task.Delay(800);
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
            }
            catch
            {
                ErrorMessage = "Sem conexao e falha ao salvar localmente.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro ao registrar manual: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            // Limpa foto da memoria assim que possivel — nao mantemos cache.
            PhotoBase64 = null;
            HasPhoto = false;
        }
    }
}
