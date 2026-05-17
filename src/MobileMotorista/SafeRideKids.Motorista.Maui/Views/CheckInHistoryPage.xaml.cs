using SafeRideKids.Motorista.Maui.ViewModels;

namespace SafeRideKids.Motorista.Maui.Views;

public partial class CheckInHistoryPage : ContentPage
{
    private readonly CheckInHistoryViewModel _vm;

    public CheckInHistoryPage(CheckInHistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
