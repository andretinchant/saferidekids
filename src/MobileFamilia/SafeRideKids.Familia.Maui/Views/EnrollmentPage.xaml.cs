using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class EnrollmentPage : ContentPage
{
    public EnrollmentPage(EnrollmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
