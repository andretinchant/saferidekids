using Microsoft.Maui.Networking;

namespace SafeRideKids.Motorista.Maui.Services;

public interface IConnectivityWatcher
{
    bool IsOnline { get; }
    event EventHandler<bool>? ConnectivityChanged;
    void Start();
    void Stop();
}

// Wrapper fino sobre Connectivity.Default. Expoe um event simples para o resto do app
// (offline queue, banners, etc).
public sealed class ConnectivityWatcher : IConnectivityWatcher, IDisposable
{
    private bool _started;

    public bool IsOnline => Connectivity.Default.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? ConnectivityChanged;

    public void Start()
    {
        if (_started) return;
        Connectivity.Default.ConnectivityChanged += OnConnectivityChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        Connectivity.Default.ConnectivityChanged -= OnConnectivityChanged;
        _started = false;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        ConnectivityChanged?.Invoke(this, e.NetworkAccess == NetworkAccess.Internet);
    }

    public void Dispose() => Stop();
}
