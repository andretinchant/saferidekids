using SafeRideKids.Motorista.Maui.ViewModels;

namespace SafeRideKids.Motorista.Maui.Views;

public partial class CheckInPage : ContentPage
{
    public CheckInPage(CheckInViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
