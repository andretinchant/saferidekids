using Microsoft.Extensions.Logging;

namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Armazena o token JWT no SecureStorage (Keystore Android / Keychain iOS).
/// Nunca armazena PII clara — token apenas. Lembrar que SecureStorage tem
/// limite prático de ~256 caracteres em algumas plataformas; tokens longos
/// devem ser refresh-tokens enxutos.
/// </summary>
public interface IAuthService
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task SetTokensAsync(string accessToken, string? refreshToken, CancellationToken cancellationToken = default);
    Task ClearAsync();
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private const string AccessTokenKey = "srk.familia.accessToken";
    private const string RefreshTokenKey = "srk.familia.refreshToken";

    private readonly ILogger<AuthService> _logger;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(AccessTokenKey).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // SecureStorage pode falhar em plataformas sem hardware-backed keystore.
            _logger.LogWarning(ex, "Falha ao ler accessToken do SecureStorage");
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string? refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar tokens no SecureStorage");
            throw;
        }
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrEmpty(token);
    }
}

/// <summary>
/// DelegatingHandler que injeta o Bearer token nas chamadas HTTP.
/// Inserido na pipeline via AddRefitClient(...).AddHttpMessageHandler&lt;BearerTokenHandler&gt;().
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IAuthService _authService;

    public BearerTokenHandler(IAuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
