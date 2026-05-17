using SafeRideKids.Motorista.Maui.ViewModels;

namespace SafeRideKids.Motorista.Maui.Views;

public partial class FallbackManualPage : ContentPage
{
    public FallbackManualPage(FallbackManualViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
