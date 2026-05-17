using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class ChildDetailPage : ContentPage
{
    private readonly ChildDetailViewModel _viewModel;

    public ChildDetailPage(ChildDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync().ConfigureAwait(true);
    }
}
