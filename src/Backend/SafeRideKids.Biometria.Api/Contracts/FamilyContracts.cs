using System;
using System.Collections.Generic;

namespace SafeRideKids.Biometria.Api.Contracts;

// Família — endpoints 6.1 da CONTRACTS.md

public sealed record FamilyRegisterRequest(string Email, string Name, string TenantId);
public sealed record FamilyRegisterResponse(Guid FamilyId);

public sealed record FamilyChildCreateRequest(
    Guid FamilyId,
    string DisplayName,
    short BirthYear,
    string School,
    string BoardingMode);

public sealed record FamilyChildCreateResponse(Guid ChildId);

public sealed record FamilyConsentRequest(
    Guid ChildId,
    string GrantedBy,
    string Cpf,
    string ConsentTextVersion);

public sealed record FamilyConsentResponse(Guid ConsentId);

public sealed record EnrollmentSummary(
    string ProviderId,
    string Status,
    string? ProviderReferenceId,
    string? ErrorCode);

public sealed record FamilyEnrollResponse(IReadOnlyList<EnrollmentSummary> Enrollments);

public sealed record FamilyEnrollmentStatusResponse(
    Guid ChildId,
    IReadOnlyList<EnrollmentSummary> Enrollments,
    DateTimeOffset? ExpiresAt);
