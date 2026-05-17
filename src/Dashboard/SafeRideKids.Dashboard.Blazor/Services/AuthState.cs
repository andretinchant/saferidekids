using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace SafeRideKids.Dashboard.Blazor.Services;

/// <summary>
/// Estado de autenticação por circuito Blazor Server.
/// O token JWT é capturado na primeira chamada HTTP (durante prerendering) via
/// <see cref="IHttpContextAccessor"/> e cacheado em memória do circuito.
/// Depois do prerender, o HttpContext não está mais disponível — usamos o cache.
/// </summary>
public interface IAuthState
{
    /// <summary>Captura/atualiza o token a partir do HttpContext atual (se houver).</summary>
    Task EnsureCachedAsync(CancellationToken cancellationToken = default);

    /// <summary>Token JWT do operador logado (Cognito) — null se não autenticado.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>HMAC curto do user id (sub claim) — usado para auditoria sem expor PII.</summary>
    string GetOperatorIdHash();

    /// <summary>Email do operador (claim) — usado para exibição no header.</summary>
    string? GetOperatorEmail();
}

public sealed class AuthState : IAuthState
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthState> _logger;

    // Cache do token e claims relevantes — escopo por circuito (Scoped DI).
    private string? _cachedAccessToken;
    private string? _cachedSub;
    private string? _cachedEmail;
    private bool _cacheInitialized;

    public AuthState(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthState> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task EnsureCachedAsync(CancellationToken cancellationToken = default)
    {
        if (_cacheInitialized) return;

        // 1) Cache claims do AuthenticationStateProvider (sempre disponível em Blazor).
        try
        {
            var state = await _authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
            _cachedSub = state.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? state.User.FindFirstValue("sub");
            _cachedEmail = state.User.FindFirstValue(ClaimTypes.Email)
                           ?? state.User.FindFirstValue("email");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AuthenticationStateProvider indisponível");
        }

        // 2) Token só está acessível durante o request HTTP (prerender).
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            try
            {
                _cachedAccessToken = await ctx.GetTokenAsync("access_token").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao ler access_token do HttpContext");
            }
        }

        _cacheInitialized = true;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCachedAsync(cancellationToken).ConfigureAwait(false);
        return _cachedAccessToken;
    }

    public string GetOperatorIdHash()
    {
        if (string.IsNullOrEmpty(_cachedSub))
        {
            return "anonymous";
        }

        // Hash determinístico não-criptográfico — POC.
        // Produção deve usar HMAC-SHA256 com tenant_salt do Secrets Manager.
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(_cachedSub);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    public string? GetOperatorEmail() => _cachedEmail;
}

/// <summary>
/// DelegatingHandler que injeta Bearer token nas chamadas Refit ao backend.
/// Lê o token do <see cref="IAuthState"/> (cache de circuito).
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IAuthState _authState;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(IAuthState authState, ILogger<BearerTokenHandler> logger)
    {
        _authState = authState;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _authState.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogDebug("Sem token Bearer para a chamada {Url}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
