using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
