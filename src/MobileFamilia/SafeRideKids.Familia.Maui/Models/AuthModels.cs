namespace SafeRideKids.Familia.Maui.Models;

// DTOs de autenticação. Esta é uma POC: o backend espera-se que exponha
// um endpoint compatível com OAuth2 password grant ou Cognito.
// Mantemos o contrato mínimo aqui — quando o backend define algo diferente,
// adaptar via Refit attributes.

public sealed record LoginRequest(string Email, string Password, string TenantId);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresInSeconds,
    string TokenType);
