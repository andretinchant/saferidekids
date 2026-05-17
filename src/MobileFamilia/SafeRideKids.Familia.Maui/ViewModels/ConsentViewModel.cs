using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// Tela de consentimento LGPD destacada. Gate antes de enrollment.
/// Regras:
///  - Texto integral exibido e o usuário deve rolar até o fim (ScrolledToEnd = true)
///    para a checkbox ficar habilitada.
///  - CPF mascarado, validado por dígitos verificadores (não consulta Receita).
///  - Assinatura simbólica: timestamp + hash SHA-256 do texto exibido.
///  - POST /api/v1/family/consent retorna consentId.
/// </summary>
[QueryProperty(nameof(ChildId), "childId")]
public sealed partial class ConsentViewModel : ObservableObject
{
    private readonly IBackendApi _api;
    private readonly IConsentTextProvider _consentTextProvider;
    private readonly ILogger<ConsentViewModel> _logger;

    [ObservableProperty] private string childId = string.Empty;
    [ObservableProperty] private string consentTextBody = string.Empty;
    [ObservableProperty] private string consentTextVersion = string.Empty;
    [ObservableProperty] private string consentTextHash = string.Empty;
    [ObservableProperty] private string grantedBy = string.Empty;
    [ObservableProperty] private string cpfMasked = string.Empty;
    [ObservableProperty] private bool hasScrolledToEnd;
    [ObservableProperty] private bool acceptChecked;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? validationMessage;

    public ConsentViewModel(
        IBackendApi api,
        IConsentTextProvider consentTextProvider,
        ILogger<ConsentViewModel> logger)
    {
        _api = api;
        _consentTextProvider = consentTextProvider;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var text = await _consentTextProvider.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        ConsentTextBody = text.Body;
        ConsentTextVersion = text.Version;
        ConsentTextHash = text.Sha256Hash;
    }

    [RelayCommand]
    private void MarkScrolledToEnd()
    {
        if (!HasScrolledToEnd)
        {
            HasScrolledToEnd = true;
            SubmitCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void OnCpfTextChanged(string raw)
    {
        var masked = CpfValidator.Mask(raw ?? string.Empty);
        if (masked != CpfMasked) CpfMasked = masked;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        ValidationMessage = null;

        if (string.IsNullOrWhiteSpace(GrantedBy) || GrantedBy.Trim().Length < 3)
        {
            ValidationMessage = "Informe seu nome completo.";
            return;
        }
        if (!CpfValidator.IsValid(CpfMasked))
        {
            ValidationMessage = "CPF inválido.";
            return;
        }

        IsBusy = true;
        try
        {
            var req = new ConsentRequest(
                ChildId: ChildId,
                GrantedBy: GrantedBy.Trim(),
                Cpf: CpfValidator.OnlyDigits(CpfMasked),
                ConsentTextVersion: ConsentTextVersion,
                ConsentTextHash: ConsentTextHash,
                SignedAtUtc: DateTimeOffset.UtcNow.ToString("O"));

            string consentId;
            try
            {
                var resp = await _api.SubmitConsentAsync(req, cancellationToken).ConfigureAwait(false);
                consentId = resp.ConsentId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Submissão de consent falhou — gerando id local (POC).");
                consentId = $"local-consent-{Guid.NewGuid():N}";
            }

            var route = $"enrollment?childId={Uri.EscapeDataString(ChildId)}&consentId={Uri.EscapeDataString(consentId)}";
            await Shell.Current.GoToAsync(route).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() => !IsBusy && HasScrolledToEnd && AcceptChecked;

    partial void OnHasScrolledToEndChanged(bool value) => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnAcceptCheckedChanged(bool value) => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => SubmitCommand.NotifyCanExecuteChanged();
}
