using SafeRideKids.Familia.Maui.ViewModels;

namespace SafeRideKids.Familia.Maui.Views;

public partial class ConsentPage : ContentPage
{
    // Margem em pixels para considerar "rolou até o fim" — telas variam.
    private const double ScrolledToEndTolerance = 24.0;

    private readonly ConsentViewModel _viewModel;

    public ConsentPage(ConsentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync().ConfigureAwait(true);

        // Mede o conteúdo após layout para garantir que ScrollY/ContentSize
        // já estão disponíveis quando o usuário começar a rolar.
        await Task.Yield();

        // Caso o texto seja menor que o viewport, libera imediatamente.
        if (ConsentScroll.ContentSize.Height <= ConsentScroll.Height + 1)
        {
            _viewModel.MarkScrolledToEndCommand.Execute(null);
        }
    }

    private void OnConsentScrolled(object? sender, ScrolledEventArgs e)
    {
        var sv = (ScrollView)sender!;
        var remaining = sv.ContentSize.Height - (e.ScrollY + sv.Height);
        if (remaining <= ScrolledToEndTolerance)
        {
            _viewModel.MarkScrolledToEndCommand.Execute(null);
        }
    }

    private void OnCpfTextChanged(object? sender, TextChangedEventArgs e)
    {
        // A máscara é aplicada no VM; aqui apenas reposiciona o cursor.
        _viewModel.OnCpfTextChangedCommand.Execute(e.NewTextValue ?? string.Empty);
    }
}
