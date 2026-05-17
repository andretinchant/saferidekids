using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class AddChildPage : ContentPage
{
    public AddChildPage(AddChildViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
