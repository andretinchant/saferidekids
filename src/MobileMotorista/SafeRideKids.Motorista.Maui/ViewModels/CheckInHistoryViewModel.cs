using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeRideKids.Motorista.Maui.Models;
using SafeRideKids.Motorista.Maui.Services;

namespace SafeRideKids.Motorista.Maui.ViewModels;

// Mostra os check-ins do dia ja registrados na sessao (in-memory) e os pendentes (SQLite).
// Para a POC, fonte unica e o SQLite + cache em memoria; em producao viria do backend.
public sealed partial class CheckInHistoryViewModel : BaseViewModel
{
    private readonly IOfflineQueueService _offlineQueue;

    public CheckInHistoryViewModel(IOfflineQueueService offlineQueue)
    {
        _offlineQueue = offlineQueue;
    }

    public ObservableCollection<CheckInHistoryItem> Items { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Items.Clear();
            var pending = await _offlineQueue.GetAllForTodayAsync();
            foreach (var p in pending.OrderByDescending(r => r.CreatedAtUtc))
            {
                Items.Add(new CheckInHistoryItem
                {
                    CheckInId = p.CheckInId,
                    ChildDisplayName = string.IsNullOrEmpty(p.ChildId) ? "(sem nome)" : p.ChildId,
                    AddressLabel = string.Empty,
                    OccurredAtLocal = p.CreatedAtUtc.ToLocalTime(),
                    Outcome = CheckInOutcome.Approved,
                    UsedFallback = true,
                    FallbackType = "manual",
                    SyncedToBackend = p.Status == "synced"
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
