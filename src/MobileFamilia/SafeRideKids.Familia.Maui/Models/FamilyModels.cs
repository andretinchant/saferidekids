namespace SafeRideKids.Familia.Maui.Models;

// DTOs do agregado Família — espelham CONTRACTS.md Seção 6.1.
// Todos os campos vão e voltam em JSON. Nada de PII deve ser persistido
// no dispositivo (ver AuthService e regra geral em CONTRACTS Seção 8).

public sealed record RegisterFamilyRequest(string Email, string Name, string TenantId);
public sealed record RegisterFamilyResponse(string FamilyId);

public sealed record CreateChildRequest(
    string FamilyId,
    string DisplayName,
    int BirthYear,
    string School,
    string BoardingMode);

public sealed record CreateChildResponse(string ChildId);

public sealed record ConsentRequest(
    string ChildId,
    string GrantedBy,
    string Cpf,                  // CPF cru — o backend hasheia. Não cachear localmente.
    string ConsentTextVersion,   // ex.: "v1"
    string ConsentTextHash,      // hash SHA-256 do texto exato exibido (assinatura simbólica)
    string SignedAtUtc);         // timestamp ISO-8601 de quando o usuário aceitou

public sealed record ConsentResponse(string ConsentId);

// Status de enrollment retornado pelo backend após upload das fotos.
// Lista um item por provedor (AWS Rekognition, Unico) — orquestração em paralelo.
public sealed record EnrollmentProviderStatus(
    string ProviderId,
    string Status,                // "active" | "expired" | "deleted" | "failed"
    string? ProviderReferenceId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record EnrollmentResponse(
    string ChildId,
    IReadOnlyList<EnrollmentProviderStatus> Enrollments,
    string? OverallStatus,
    string? ExpiresAtUtc);

public sealed record ChildEnrollmentStatus(
    string ChildId,
    string DisplayName,
    string Status,                // "active" | "expired" | "none"
    DateTimeOffset? EnrolledAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<EnrollmentProviderStatus> Providers);

// Sumário leve para a tela "Minhas crianças". O backend pode devolver
// um GET futuramente; por enquanto compomos a partir do CreateChild + status.
public sealed record ChildSummary(
    string ChildId,
    string DisplayName,
    int BirthYear,
    string School,
    string BoardingMode,
    string EnrollmentStatus,
    DateTimeOffset? EnrollmentExpiresAt);
