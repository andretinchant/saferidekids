using CommunityToolkit.Mvvm.ComponentModel;

namespace SafeRideKids.Motorista.Maui.ViewModels;

// Base com sinalizacoes de busy/error para todas as paginas.
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? infoMessage;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        InfoMessage = null;
    }
}
