namespace SafeRideKids.Dashboard.Blazor.Models;

// DTOs para revisão operacional de famílias/crianças.
// Backend devolve apenas iniciais e hashes — nada de PII clara em listagens.
// Para detalhes (decifrar nome), o operador precisa solicitar via endpoint
// específico que registra acesso em audit_log.

public sealed record FamilyListItem(
    Guid Id,
    string TenantId,
    string ResponsavelEmailHashShort,    // primeiros 8 chars do HMAC — exibição segura
    string? PreferredProviderId,
    int ChildrenCount,
    int ActiveEnrollmentsCount,
    DateTimeOffset CreatedAt);

public sealed record ChildListItem(
    Guid Id,
    Guid FamilyId,
    string DisplayInitials,              // ex.: "M.A.S." — default seguro
    int BirthYear,
    string School,
    string BoardingMode,                 // manual | facial | tag
    IReadOnlyList<ChildEnrollmentProviderStatus> Enrollments,
    DateTimeOffset CreatedAt);

public sealed record ChildEnrollmentProviderStatus(
    string ProviderId,                   // aws-rekognition | unico-idcloud
    string Status,                       // active | expired | deleted | none | failed
    DateTimeOffset? EnrolledAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Resposta da consulta "revelar nome" — usar com parcimônia, registra audit_log.
/// O backend devolve o display_name decifrado em runtime. Não cachear.
/// </summary>
public sealed record RevealedDisplayName(
    Guid ChildId,
    string DisplayName,                  // claro — não persistir client-side
    DateTimeOffset RevealedAt,
    Guid AuditEntryId);
