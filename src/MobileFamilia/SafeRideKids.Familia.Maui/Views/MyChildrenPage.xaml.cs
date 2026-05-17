using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class MyChildrenPage : ContentPage
{
    public MyChildrenPage(MyChildrenViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
