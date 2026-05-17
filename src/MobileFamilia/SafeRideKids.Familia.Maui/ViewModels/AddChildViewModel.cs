using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// Cadastro de criança. Valida idade 4..17 anos. Modo de embarque: manual|facial|tag
/// (conforme schema biometria_poc.child em CONTRACTS Seção 5).
/// </summary>
public sealed partial class AddChildViewModel : ObservableObject
{
    private const int MinAge = 4;
    private const int MaxAge = 17;

    private readonly IBackendApi _api;
    private readonly IChildrenStore _store;
    private readonly ILogger<AddChildViewModel> _logger;

    [ObservableProperty] private string displayName = string.Empty;
    [ObservableProperty] private string birthYearText = string.Empty;
    [ObservableProperty] private string school = string.Empty;
    [ObservableProperty] private string boardingMode = "facial"; // default p/ POC biometria
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? validationMessage;

    public IReadOnlyList<string> BoardingModes { get; } = new[] { "manual", "facial", "tag" };

    public AddChildViewModel(IBackendApi api, IChildrenStore store, ILogger<AddChildViewModel> logger)
    {
        _api = api;
        _store = store;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        ValidationMessage = null;

        if (!int.TryParse(BirthYearText, out var birthYear))
        {
            ValidationMessage = "Informe o ano de nascimento (apenas dígitos).";
            return;
        }

        var currentYear = DateTime.UtcNow.Year;
        var age = currentYear - birthYear;
        if (age < MinAge || age > MaxAge)
        {
            ValidationMessage = $"Idade deve estar entre {MinAge} e {MaxAge} anos.";
            return;
        }

        if (DisplayName.Trim().Length < 2)
        {
            ValidationMessage = "Nome muito curto.";
            return;
        }
        if (School.Trim().Length < 2)
        {
            ValidationMessage = "Informe o nome da escola.";
            return;
        }
        if (!BoardingModes.Contains(BoardingMode))
        {
            ValidationMessage = "Modo de embarque inválido.";
            return;
        }

        IsBusy = true;
        try
        {
            // POC: familyId real virá do backend após /family/register.
            // Aqui usamos placeholder estável "poc-family" — o backend pode
            // resolver via token, ou aceitamos e o agente do backend ajusta.
            var req = new CreateChildRequest(
                FamilyId: "poc-family",
                DisplayName: DisplayName.Trim(),
                BirthYear: birthYear,
                School: School.Trim(),
                BoardingMode: BoardingMode);

            string childId;
            try
            {
                var resp = await _api.CreateChildAsync(req, cancellationToken).ConfigureAwait(false);
                childId = resp.ChildId;
            }
            catch (Exception ex)
            {
                // Modo offline POC — gera id local.
                _logger.LogWarning(ex, "CreateChild backend falhou — gerando id local (POC).");
                childId = $"local-{Guid.NewGuid():N}";
            }

            var summary = new ChildSummary(
                ChildId: childId,
                DisplayName: req.DisplayName,
                BirthYear: req.BirthYear,
                School: req.School,
                BoardingMode: req.BoardingMode,
                EnrollmentStatus: "none",
                EnrollmentExpiresAt: null);

            _store.Upsert(summary);

            // Próximo passo do fluxo: consentimento antes do enrollment.
            await Shell.Current.GoToAsync($"consent?childId={Uri.EscapeDataString(childId)}")
                .ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() => !IsBusy
                                && !string.IsNullOrWhiteSpace(DisplayName)
                                && !string.IsNullOrWhiteSpace(BirthYearText)
                                && !string.IsNullOrWhiteSpace(School);

    partial void OnDisplayNameChanged(string value) => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnBirthYearTextChanged(string value) => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnSchoolChanged(string value) => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => SubmitCommand.NotifyCanExecuteChanged();
}
