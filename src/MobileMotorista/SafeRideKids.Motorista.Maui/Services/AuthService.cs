using System.Net.Http.Headers;
using SafeRideKids.Motorista.Maui.Models;

namespace SafeRideKids.Motorista.Maui.Services;

public interface IAuthService
{
    // Token persistido em SecureStorage. Pode ser null se nao autenticado.
    Task<string?> GetAccessTokenAsync();

    // Mock para a POC: nao bate em Cognito de verdade. Persiste um token fake.
    // Em producao trocar por chamada real ao IBackendApi.LoginAsync.
    Task<LoginResponse> LoginMockAsync(string email, string password, string tenantId);

    Task LogoutAsync();

    string? CurrentMotoristaId { get; }
    string? CurrentDisplayName { get; }
}

// AuthService POC: para nao depender do backend de auth ainda, gera token local.
// TODO[backend-auth]: substituir LoginMockAsync por IBackendApi.LoginAsync quando o
// endpoint de Cognito estiver disponivel (CONTRACTS Secao 13).
public sealed class AuthService : IAuthService
{
    private const string TokenKey = "saferide.motorista.token";
    private const string MotoristaIdKey = "saferide.motorista.id";
    private const string DisplayNameKey = "saferide.motorista.displayName";

    public string? CurrentMotoristaId { get; private set; }
    public string? CurrentDisplayName { get; private set; }

    public async Task<string?> GetAccessTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            CurrentMotoristaId ??= await SecureStorage.Default.GetAsync(MotoristaIdKey);
            CurrentDisplayName ??= await SecureStorage.Default.GetAsync(DisplayNameKey);
        }
        return token;
    }

    public async Task<LoginResponse> LoginMockAsync(string email, string password, string tenantId)
    {
        // Para a POC, qualquer credencial vazia falha; senao gera token simulado.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Credenciais invalidas.");
        }

        // Token opaco — JWT real virara do Cognito mais tarde.
        var fakeToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var motoristaId = "mock-" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant()[..12];
        var displayName = email.Split('@')[0];

        await SecureStorage.Default.SetAsync(TokenKey, fakeToken);
        await SecureStorage.Default.SetAsync(MotoristaIdKey, motoristaId);
        await SecureStorage.Default.SetAsync(DisplayNameKey, displayName);

        CurrentMotoristaId = motoristaId;
        CurrentDisplayName = displayName;

        return new LoginResponse(
            AccessToken: fakeToken,
            RefreshToken: null,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(8),
            MotoristaId: motoristaId,
            DisplayName: displayName);
    }

    public Task LogoutAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(MotoristaIdKey);
        SecureStorage.Default.Remove(DisplayNameKey);
        CurrentMotoristaId = null;
        CurrentDisplayName = null;
        return Task.CompletedTask;
    }
}

// Handler HTTP que injeta Authorization: Bearer <token> em toda requisicao.
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly IAuthService _auth;

    public AuthHeaderHandler(IAuthService auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
