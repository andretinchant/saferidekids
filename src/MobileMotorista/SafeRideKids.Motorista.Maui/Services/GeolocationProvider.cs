namespace SafeRideKids.Motorista.Maui.Services;

public interface IGeolocationProvider
{
    Task<(double Lat, double Lng)?> TryGetCurrentLocationAsync(CancellationToken cancellationToken);
}

// Encapsula a chamada a Geolocation.Default para facilitar mock em testes.
// Em silencio se nao houver permissao — o backend aceita geo opcional.
public sealed class GeolocationProvider : IGeolocationProvider
{
    public async Task<(double Lat, double Lng)?> TryGetCurrentLocationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Primeiro tenta o last known (rapido, sem GPS warm-up).
            var last = await Geolocation.Default.GetLastKnownLocationAsync();
            if (last is not null)
            {
                return (last.Latitude, last.Longitude);
            }

            // Se nao houver, faz uma leitura curta. Timeout para nao travar o fluxo.
            var current = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)),
                cancellationToken);
            return current is null ? null : (current.Latitude, current.Longitude);
        }
        catch
        {
            // Sem permissao, sem GPS, sem rede — geo e opcional no contrato.
            return null;
        }
    }
}
