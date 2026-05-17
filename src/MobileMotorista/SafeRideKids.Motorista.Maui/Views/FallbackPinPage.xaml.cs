using SafeRideKids.Motorista.Maui.ViewModels;

namespace SafeRideKids.Motorista.Maui.Views;

public partial class FallbackPinPage : ContentPage
{
    public FallbackPinPage(FallbackPinViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
