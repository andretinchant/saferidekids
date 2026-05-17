using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Refit;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// Wizard de enrollment facial: 3 a 5 fotos, guia por etapa.
/// Fotos ficam apenas em memória (List<byte[]>) e são enviadas via multipart.
/// </summary>
[QueryProperty(nameof(ChildId), "childId")]
[QueryProperty(nameof(ConsentId), "consentId")]
public sealed partial class EnrollmentViewModel : ObservableObject
{
    private const int MinPhotos = 3;
    private const int MaxPhotos = 5;

    private readonly IBackendApi _api;
    private readonly ICameraCaptureService _camera;
    private readonly IChildrenStore _store;
    private readonly ILogger<EnrollmentViewModel> _logger;

    [ObservableProperty] private string childId = string.Empty;
    [ObservableProperty] private string consentId = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private int currentStepIndex;

    public ObservableCollection<CapturedFrame> Captures { get; } = new();
    public ObservableCollection<EnrollmentProviderStatus> Results { get; } = new();

    // Roteiro de captura — guia visual para o responsável.
    public IReadOnlyList<string> StepInstructions { get; } = new[]
    {
        "Posicione a criança olhando para frente, em ambiente bem iluminado.",
        "Peça à criança para virar a cabeça devagar para a esquerda.",
        "Agora vire devagar para a direita.",
        "Sorriso leve, olhando para a câmera (opcional).",
        "Expressão neutra, olhando para a câmera (opcional)."
    };

    public string CurrentInstruction =>
        CurrentStepIndex < StepInstructions.Count
            ? StepInstructions[CurrentStepIndex]
            : "Pronto. Você pode enviar agora.";

    public int MinPhotosCount => MinPhotos;
    public int MaxPhotosCount => MaxPhotos;
    public int CapturedCount => Captures.Count;
    public bool HasMinimum => Captures.Count >= MinPhotos;
    public bool CanCaptureMore => Captures.Count < MaxPhotos;

    public EnrollmentViewModel(
        IBackendApi api,
        ICameraCaptureService camera,
        IChildrenStore store,
        ILogger<EnrollmentViewModel> logger)
    {
        _api = api;
        _camera = camera;
        _store = store;
        _logger = logger;

        Captures.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CapturedCount));
            OnPropertyChanged(nameof(HasMinimum));
            OnPropertyChanged(nameof(CanCaptureMore));
            CaptureCommand.NotifyCanExecuteChanged();
            UploadCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task CaptureAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            if (!_camera.IsCaptureSupported)
            {
                StatusMessage = "Câmera não disponível neste dispositivo.";
                return;
            }

            var photo = await _camera.CaptureAsync(cancellationToken).ConfigureAwait(false);
            if (photo is null)
            {
                StatusMessage = "Captura cancelada ou negada.";
                return;
            }

            Captures.Add(new CapturedFrame(Index: Captures.Count + 1, JpegBytes: photo.JpegBytes));
            if (CurrentStepIndex < StepInstructions.Count - 1) CurrentStepIndex++;
            OnPropertyChanged(nameof(CurrentInstruction));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCapture() => !IsBusy && CanCaptureMore;

    [RelayCommand]
    private void RetakeLast()
    {
        if (Captures.Count == 0) return;
        Captures.RemoveAt(Captures.Count - 1);
        if (CurrentStepIndex > 0) CurrentStepIndex--;
        OnPropertyChanged(nameof(CurrentInstruction));
    }

    [RelayCommand]
    private void RemoveAt(int index)
    {
        if (index < 0 || index >= Captures.Count) return;
        Captures.RemoveAt(index);
        // Reindexa para o usuário ver 1..n
        for (var i = 0; i < Captures.Count; i++) Captures[i] = Captures[i] with { Index = i + 1 };
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        if (Captures.Count < MinPhotos || Captures.Count > MaxPhotos)
        {
            StatusMessage = $"Precisa entre {MinPhotos} e {MaxPhotos} fotos.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Enviando…";
        Results.Clear();
        try
        {
            var parts = new List<StreamPart>(Captures.Count);
            try
            {
                for (var i = 0; i < Captures.Count; i++)
                {
                    var bytes = Captures[i].JpegBytes;
                    var ms = new MemoryStream(bytes, writable: false);
                    parts.Add(new StreamPart(ms, fileName: $"capture-{i + 1}.jpg", contentType: "image/jpeg"));
                }

                var response = await _api.EnrollChildAsync(
                    ChildId, ConsentId, parts, cancellationToken).ConfigureAwait(false);

                foreach (var r in response.Enrollments) Results.Add(r);

                // Atualiza store local
                var existing = _store.Find(ChildId);
                if (existing is not null)
                {
                    var status = response.OverallStatus ?? (response.Enrollments.Any(e => e.Status == "active") ? "active" : "failed");
                    var expires = DateTimeOffset.TryParse(response.ExpiresAtUtc, out var dt) ? dt : (DateTimeOffset?)null;
                    _store.Upsert(existing with { EnrollmentStatus = status, EnrollmentExpiresAt = expires });
                }

                StatusMessage = response.Enrollments.Any(e => e.Status == "active")
                    ? "Cadastro concluído em pelo menos um provedor."
                    : "Backend retornou sem provedores ativos.";
            }
            finally
            {
                foreach (var p in parts)
                {
                    try { p.Value.Dispose(); } catch { /* best-effort */ }
                }
            }

            await Task.Delay(800, cancellationToken).ConfigureAwait(false);
            await Shell.Current.GoToAsync("//children").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload de enrollment falhou");
            StatusMessage = "Falha ao enviar. Tente novamente em alguns instantes.";
        }
        finally
        {
            // Limpa buffers para reduzir tempo em memória.
            Captures.Clear();
            IsBusy = false;
        }
    }

    private bool CanUpload() => !IsBusy && HasMinimum;

    partial void OnIsBusyChanged(bool value)
    {
        CaptureCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>Foto capturada — permanece apenas em memória até o upload.</summary>
public sealed record CapturedFrame(int Index, byte[] JpegBytes);
