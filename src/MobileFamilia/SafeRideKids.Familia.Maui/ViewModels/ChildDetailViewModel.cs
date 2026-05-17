using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// Detalhe da criança + revogação de consentimento (DSR-delete).
/// Dupla confirmação antes de chamar DELETE /api/v1/family/children/{id}/enrollment.
/// </summary>
[QueryProperty(nameof(ChildId), "childId")]
public sealed partial class ChildDetailViewModel : ObservableObject
{
    private readonly IBackendApi _api;
    private readonly IChildrenStore _store;
    private readonly ILogger<ChildDetailViewModel> _logger;

    [ObservableProperty] private string childId = string.Empty;
    [ObservableProperty] private ChildSummary? child;
    [ObservableProperty] private ChildEnrollmentStatus? enrollment;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;

    public ChildDetailViewModel(IBackendApi api, IChildrenStore store, ILogger<ChildDetailViewModel> logger)
    {
        _api = api;
        _store = store;
        _logger = logger;
    }

    partial void OnChildIdChanged(string value) => _ = LoadAsync();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ChildId)) return;
        Child = _store.Find(ChildId);
        try
        {
            Enrollment = await _api.GetChildEnrollmentAsync(ChildId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Status de enrollment indisponível (backend pode estar off na POC).");
        }
    }

    [RelayCommand]
    private async Task RecaptureAsync()
    {
        // Recapturar exige consent ativo. O fluxo correto é abrir o consent de novo.
        await Shell.Current.GoToAsync($"consent?childId={Uri.EscapeDataString(ChildId)}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RevokeAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;

        // Dupla confirmação — exigência do escopo
        var firstOk = await Shell.Current.DisplayAlert(
            title: "Remover biometria?",
            message: "Esta ação remove os cadastros faciais da criança em todos os provedores. Você pode refazer depois.",
            accept: "Continuar",
            cancel: "Cancelar").ConfigureAwait(false);
        if (!firstOk) return;

        var secondOk = await Shell.Current.DisplayAlert(
            title: "Confirma definitivamente?",
            message: "Esta operação é irreversível para o cadastro atual.",
            accept: "Sim, remover",
            cancel: "Não").ConfigureAwait(false);
        if (!secondOk) return;

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _api.DeleteChildEnrollmentAsync(ChildId, cancellationToken).ConfigureAwait(false);
            var existing = _store.Find(ChildId);
            if (existing is not null)
            {
                _store.Upsert(existing with { EnrollmentStatus = "none", EnrollmentExpiresAt = null });
            }
            StatusMessage = "Biometria removida.";
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            await Shell.Current.GoToAsync("..").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao remover biometria");
            StatusMessage = "Falha ao remover. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
