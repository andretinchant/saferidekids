using System;

namespace SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

/// <summary>Entidade família (responsável).</summary>
public sealed class FamilyEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ResponsavelEmailHash { get; set; } = string.Empty;
    public string? PreferredProviderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Entidade criança.</summary>
public sealed class ChildEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public byte[] DisplayNameEncrypted { get; set; } = Array.Empty<byte>();
    public short BirthYear { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string BoardingMode { get; set; } = "manual";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Entidade consentimento LGPD.</summary>
public sealed class ConsentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public Guid ChildId { get; set; }
    public string GrantedBy { get; set; } = string.Empty;
    public string ResponsavelCpfHash { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string Scope { get; set; } = "biometria_checkin";
    public string ConsentTextHash { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string SignedTextStorageKey { get; set; } = string.Empty;
}

/// <summary>Entidade enrollment biométrico.</summary>
public sealed class EnrollmentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ChildId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderReferenceId { get; set; } = string.Empty;
    public string? TemplateStorageKey { get; set; }
    public Guid ConsentId { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Entidade rota (escala diária de motorista).</summary>
public sealed class RouteEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string MotoristaId { get; set; } = string.Empty;
    public string VehiclePlate { get; set; } = string.Empty;
    public DateOnly ScheduledDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Entidade parada da rota.</summary>
public sealed class RouteStopEntity
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public Guid ChildId { get; set; }
    public short StopOrder { get; set; }
    public TimeOnly ExpectedPickupTime { get; set; }
    public string AddressLabel { get; set; } = string.Empty;
}

/// <summary>Entidade check-in (atendimento de uma parada).</summary>
public sealed class CheckInEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid RouteId { get; set; }
    public Guid RouteStopId { get; set; }
    public Guid ChildId { get; set; }
    public string MotoristaId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Result { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderSessionId { get; set; }
    public decimal? Confidence { get; set; }
    public bool? LivenessPassed { get; set; }
    public bool UsedFallback { get; set; }
    public string? FallbackType { get; set; }
    public int? LatencyMs { get; set; }
    public long? ProviderCostMicroCents { get; set; }
    public decimal? GeoLat { get; set; }
    public decimal? GeoLng { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Entidade evento do check-in (audit + UX recall).</summary>
public sealed class CheckInEventEntity
{
    public Guid Id { get; set; }
    public Guid CheckInId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>Entidade PIN de fallback.</summary>
public sealed class FallbackPinEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ChildId { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public short Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>Entidade audit log (LGPD).</summary>
public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ActorIdHash { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
    public DateOnly RetentionUntil { get; set; }
}

/// <summary>Entidade outbox de notificações.</summary>
public sealed class NotificationOutboxEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public Guid ChildId { get; set; }
    public Guid? CheckInId { get; set; }
    public string Template { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public string Status { get; set; } = "pending";   // pending | dispatched | failed
}
