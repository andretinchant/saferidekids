using SafeRideKids.Motorista.Maui.ViewModels;

namespace SafeRideKids.Motorista.Maui.Views;

public partial class LivenessCapturePage : ContentPage
{
    private readonly LivenessCaptureViewModel _vm;

    public LivenessCapturePage(LivenessCaptureViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Roda automaticamente a captura quando a pagina aparece.
        await _vm.RunAsync();
    }
}
