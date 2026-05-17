using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;
using SafeRideKids.Motorista.Maui.Views;

namespace SafeRideKids.Motorista.Maui.ViewModels;

// Roda o SDK stub de liveness e depois chama /checkin/verify.
// Decide o proximo passo conforme suggestNextAction (confirm | fallback_pin | fallback_manual).
[QueryProperty(nameof(CheckInId), nameof(CheckInId))]
[QueryProperty(nameof(ProviderId), nameof(ProviderId))]
[QueryProperty(nameof(SessionId), nameof(SessionId))]
[QueryProperty(nameof(ChildDisplayName), nameof(ChildDisplayName))]
[QueryProperty(nameof(AddressLabel), nameof(AddressLabel))]
public sealed partial class LivenessCaptureViewModel : BaseViewModel
{
    private readonly IBackendApi _api;
    private readonly ILivenessSdkOrchestrator _liveness;
    private readonly IConnectivityWatcher _connectivity;
    private readonly IHapticsService _haptics;
    private readonly IGeolocationProvider _geo;
    private readonly IOfflineQueueService _offlineQueue;

    public LivenessCaptureViewModel(
        IBackendApi api,
        ILivenessSdkOrchestrator liveness,
        IConnectivityWatcher connectivity,
        IHapticsService haptics,
        IGeolocationProvider geo,
        IOfflineQueueService offlineQueue)
    {
        _api = api;
        _liveness = liveness;
        _connectivity = connectivity;
        _haptics = haptics;
        _geo = geo;
        _offlineQueue = offlineQueue;
    }

    [ObservableProperty]
    private string checkInId = string.Empty;

    [ObservableProperty]
    private string providerId = string.Empty;

    [ObservableProperty]
    private string sessionId = string.Empty;

    [ObservableProperty]
    private string childDisplayName = string.Empty;

    [ObservableProperty]
    private string addressLabel = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Preparando captura...";

    [ObservableProperty]
    private string providerLabel = string.Empty;

    [ObservableProperty]
    private bool isCapturing;

    [RelayCommand]
    public async Task RunAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        IsBusy = true;
        IsCapturing = true;
        ProviderLabel = ProviderId switch
        {
            "aws-rekognition" => "AWS Rekognition",
            "unico-idcloud" => "Unico IDCloud",
            _ => ProviderId
        };

        try
        {
            var progress = new Progress<string>(msg => StatusMessage = msg);

            // Simula chamada do SDK. Em producao chama o SDK nativo via partial class por OS.
            var sdkSessionInfo = new LivenessSessionInfoDto(
                SessionId: SessionId,
                SdkConfig: new Dictionary<string, string>(),
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(2));

            var sdkResult = await _liveness.RunLivenessAsync(
                ProviderId, sdkSessionInfo, progress, CancellationToken.None);

            if (!sdkResult.Captured)
            {
                // Timeout 30s ou erro do SDK -> fallback automatico.
                StatusMessage = "Captura nao concluida.";
                _haptics.Warning();
                if (sdkResult.FailureReason == "timeout_30s")
                {
                    InfoMessage = "Tempo esgotado. Usando fallback PIN.";
                    await NavigateToFallbackPinAsync();
                }
                else
                {
                    InfoMessage = "Falha no SDK. Usando fallback manual.";
                    await NavigateToFallbackManualAsync();
                }
                return;
            }

            StatusMessage = "Verificando com servidor...";
            CheckInVerifyResponse verifyResp;
            try
            {
                verifyResp = await _api.VerifyCheckInAsync(
                    new CheckInVerifyRequest(CheckInId, sdkResult.SessionId),
                    CancellationToken.None);
            }
            catch (HttpRequestException)
            {
                // Sem rede no momento de verificar -> fallback PIN (online esperado) ou manual offline.
                InfoMessage = _connectivity.IsOnline
                    ? "Falha de rede na verificacao. Usando fallback PIN."
                    : "Sem conexao. Usando fallback manual offline.";
                if (_connectivity.IsOnline)
                {
                    await NavigateToFallbackPinAsync();
                }
                else
                {
                    await NavigateToFallbackManualAsync();
                }
                return;
            }

            var outcome = CheckInOutcomeExtensions.ParseOutcome(verifyResp.Outcome);
            var suggested = CheckInOutcomeExtensions.ParseAction(verifyResp.SuggestNextAction);

            switch (outcome)
            {
                case CheckInOutcome.Approved when suggested == CheckInSuggestedAction.Confirm:
                    _haptics.Success();
                    InfoMessage = "Aprovado. Confirmando embarque...";
                    await NavigateToConfirmingAsync(outcome);
                    break;
                case CheckInOutcome.Inconclusive:
                    _haptics.Warning();
                    InfoMessage = "Verificacao inconclusiva. Tentar PIN do responsavel?";
                    await NavigateToFallbackPinAsync();
                    break;
                case CheckInOutcome.Rejected:
                    _haptics.Warning();
                    ErrorMessage = "Rosto nao bate. Vamos confirmar manualmente.";
                    await NavigateToFallbackManualAsync();
                    break;
                default:
                    // Outcome inesperado: cautela.
                    await NavigateToFallbackManualAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado: " + ex.Message;
            await NavigateToFallbackManualAsync();
        }
        finally
        {
            IsCapturing = false;
            IsBusy = false;
        }
    }

    private async Task NavigateToConfirmingAsync(CheckInOutcome outcome)
    {
        // Aprovado: confirmamos direto via API com geolocalizacao e voltamos para a rota.
        var geo = await _geo.TryGetCurrentLocationAsync(CancellationToken.None);

        try
        {
            await _api.ConfirmCheckInAsync(
                new CheckInConfirmRequest(CheckInId, geo?.Lat, geo?.Lng),
                CancellationToken.None);
        }
        catch
        {
            // Confirmacao falhou — registra como pendente em fila offline (apenas metadado).
            await _offlineQueue.EnqueueManualFallbackAsync(new OfflineCheckInRecord
            {
                CheckInId = CheckInId,
                RouteStopId = string.Empty,
                ChildId = string.Empty,
                Justification = "Confirmacao falhou apos approve; necessario revisar.",
                GeoLat = geo?.Lat,
                GeoLng = geo?.Lng
            });
        }

        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}/{nameof(TodayRoutePage)}");
    }

    private async Task NavigateToFallbackPinAsync()
    {
        var nav = new Dictionary<string, object>
        {
            ["CheckInId"] = CheckInId,
            ["ChildDisplayName"] = ChildDisplayName
        };
        await Shell.Current.GoToAsync(nameof(FallbackPinPage), nav);
    }

    private async Task NavigateToFallbackManualAsync()
    {
        var nav = new Dictionary<string, object>
        {
            ["CheckInId"] = CheckInId,
            ["StopId"] = string.Empty,
            ["ChildId"] = string.Empty,
            ["ChildDisplayName"] = ChildDisplayName,
            ["AddressLabel"] = AddressLabel,
            ["IsOffline"] = !_connectivity.IsOnline
        };
        await Shell.Current.GoToAsync(nameof(FallbackManualPage), nav);
    }
}
