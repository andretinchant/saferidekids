using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Familia.Maui.Models;
using SafeRideKids.Familia.Maui.Services;

namespace SafeRideKids.Familia.Maui.ViewModels;

/// <summary>
/// Lista de crianças do responsável + status do enrollment.
/// Os dados vêm do ChildrenStore (cache em memória) — a POC ainda não tem
/// endpoint GET /family/me/children no backend.
/// </summary>
public sealed partial class MyChildrenViewModel : ObservableObject
{
    private readonly IChildrenStore _store;
    private readonly IAuthService _auth;

    public ObservableCollection<ChildSummary> Children { get; } = new();

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string? statusMessage;

    public MyChildrenViewModel(IChildrenStore store, IAuthService auth)
    {
        _store = store;
        _auth = auth;
        _store.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(LoadFromStore);
        LoadFromStore();
    }

    private void LoadFromStore()
    {
        Children.Clear();
        foreach (var c in _store.Snapshot) Children.Add(c);
        StatusMessage = Children.Count == 0
            ? "Você ainda não cadastrou nenhuma criança."
            : null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            // Placeholder: quando houver GET /family/me/children, recarregar daqui.
            await Task.Delay(150).ConfigureAwait(false);
            LoadFromStore();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddChildAsync()
        => await Shell.Current.GoToAsync("addChild").ConfigureAwait(false);

    [RelayCommand]
    private async Task OpenChildAsync(ChildSummary? child)
    {
        if (child is null) return;
        var route = $"childDetail?childId={Uri.EscapeDataString(child.ChildId)}";
        await Shell.Current.GoToAsync(route).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _auth.ClearAsync().ConfigureAwait(false);
        _store.Clear();
        await Shell.Current.GoToAsync("//login").ConfigureAwait(false);
    }
}
