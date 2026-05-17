using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Infrastructure.Persistence.Configurations;

public sealed class FamilyEntityConfiguration : IEntityTypeConfiguration<FamilyEntity>
{
    public void Configure(EntityTypeBuilder<FamilyEntity> builder)
    {
        builder.ToTable("family");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ResponsavelEmailHash).HasColumnName("responsavel_email_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.PreferredProviderId).HasColumnName("preferred_provider_id").HasMaxLength(64);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.TenantId, e.ResponsavelEmailHash })
            .HasDatabaseName("family_tenant_email_uk")
            .IsUnique();
        builder.HasIndex(e => e.TenantId).HasDatabaseName("family_tenant_ix");
    }
}

public sealed class ChildEntityConfiguration : IEntityTypeConfiguration<ChildEntity>
{
    public void Configure(EntityTypeBuilder<ChildEntity> builder)
    {
        builder.ToTable("child");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.FamilyId).HasColumnName("family_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.DisplayNameEncrypted).HasColumnName("display_name_encrypted").IsRequired();
        builder.Property(e => e.BirthYear).HasColumnName("birth_year").IsRequired();
        builder.Property(e => e.SchoolName).HasColumnName("school_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.BoardingMode).HasColumnName("boarding_mode").HasMaxLength(16).HasDefaultValue("manual").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.FamilyId).HasDatabaseName("child_family_ix");

        builder.HasOne<FamilyEntity>()
            .WithMany()
            .HasForeignKey(e => e.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConsentEntityConfiguration : IEntityTypeConfiguration<ConsentEntity>
{
    public void Configure(EntityTypeBuilder<ConsentEntity> builder)
    {
        builder.ToTable("consent");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FamilyId).HasColumnName("family_id");
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.GrantedBy).HasColumnName("granted_by").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ResponsavelCpfHash).HasColumnName("responsavel_cpf_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        builder.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(64).HasDefaultValue("biometria_checkin").IsRequired();
        builder.Property(e => e.ConsentTextHash).HasColumnName("consent_text_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.IpAddress).HasColumnName("ip_address").HasColumnType("inet").IsRequired();
        builder.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500).IsRequired();
        builder.Property(e => e.SignedTextStorageKey).HasColumnName("signed_text_storage_key").HasMaxLength(500).IsRequired();

        // Index único parcial — somente consents ativos (revoked_at IS NULL) são considerados.
        // Equivalente ao "UNIQUE (child_id) WHERE revoked_at IS NULL" da Seção 5.
        builder.HasIndex(e => e.ChildId)
            .HasDatabaseName("consent_active_uk")
            .IsUnique()
            .HasFilter("revoked_at IS NULL");

        builder.HasOne<FamilyEntity>().WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChildEntity>().WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EnrollmentEntityConfiguration : IEntityTypeConfiguration<EnrollmentEntity>
{
    public void Configure(EntityTypeBuilder<EnrollmentEntity> builder)
    {
        builder.ToTable("enrollment");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.ProviderId).HasColumnName("provider_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderReferenceId).HasColumnName("provider_reference_id").HasMaxLength(200).IsRequired();
        builder.Property(e => e.TemplateStorageKey).HasColumnName("template_storage_key").HasMaxLength(500);
        builder.Property(e => e.ConsentId).HasColumnName("consent_id");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(e => e.EnrolledAt).HasColumnName("enrolled_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");

        // Unique parcial: 1 enrollment ATIVO por criança por provedor.
        builder.HasIndex(e => new { e.ChildId, e.ProviderId })
            .HasDatabaseName("enrollment_active_per_provider")
            .IsUnique()
            .HasFilter("status = 'active'");

        builder.HasIndex(e => new { e.Status, e.ExpiresAt })
            .HasDatabaseName("enrollment_status_expires_ix");

        builder.HasOne<ChildEntity>().WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsentEntity>().WithMany().HasForeignKey(e => e.ConsentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RouteEntityConfiguration : IEntityTypeConfiguration<RouteEntity>
{
    public void Configure(EntityTypeBuilder<RouteEntity> builder)
    {
        builder.ToTable("route");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.MotoristaId).HasColumnName("motorista_id").HasMaxLength(128).IsRequired();
        builder.Property(e => e.VehiclePlate).HasColumnName("vehicle_plate").HasMaxLength(16).IsRequired();
        builder.Property(e => e.ScheduledDate).HasColumnName("scheduled_date").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.MotoristaId, e.ScheduledDate }).HasDatabaseName("route_motorista_date_ix");
    }
}

public sealed class RouteStopEntityConfiguration : IEntityTypeConfiguration<RouteStopEntity>
{
    public void Configure(EntityTypeBuilder<RouteStopEntity> builder)
    {
        builder.ToTable("route_stop");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RouteId).HasColumnName("route_id");
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.StopOrder).HasColumnName("stop_order").IsRequired();
        builder.Property(e => e.ExpectedPickupTime).HasColumnName("expected_pickup_time").HasColumnType("timetz").IsRequired();
        builder.Property(e => e.AddressLabel).HasColumnName("address_label").HasMaxLength(200).IsRequired();

        builder.HasIndex(e => new { e.RouteId, e.StopOrder }).HasDatabaseName("route_stop_order_uk").IsUnique();

        builder.HasOne<RouteEntity>().WithMany().HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ChildEntity>().WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckInEntityConfiguration : IEntityTypeConfiguration<CheckInEntity>
{
    public void Configure(EntityTypeBuilder<CheckInEntity> builder)
    {
        builder.ToTable("checkin");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.RouteId).HasColumnName("route_id");
        builder.Property(e => e.RouteStopId).HasColumnName("route_stop_id");
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.MotoristaId).HasColumnName("motorista_id").HasMaxLength(128).IsRequired();
        builder.Property(e => e.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(e => e.FinishedAt).HasColumnName("finished_at");
        builder.Property(e => e.Result).HasColumnName("result").HasMaxLength(16);
        builder.Property(e => e.ProviderId).HasColumnName("provider_id").HasMaxLength(64);
        builder.Property(e => e.ProviderSessionId).HasColumnName("provider_session_id").HasMaxLength(200);
        builder.Property(e => e.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(e => e.LivenessPassed).HasColumnName("liveness_passed");
        builder.Property(e => e.UsedFallback).HasColumnName("used_fallback").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.FallbackType).HasColumnName("fallback_type").HasMaxLength(32);
        builder.Property(e => e.LatencyMs).HasColumnName("latency_ms");
        builder.Property(e => e.ProviderCostMicroCents).HasColumnName("provider_cost_microcents");
        builder.Property(e => e.GeoLat).HasColumnName("geo_lat").HasPrecision(9, 6);
        builder.Property(e => e.GeoLng).HasColumnName("geo_lng").HasPrecision(9, 6);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);

        builder.HasIndex(e => e.RouteId).HasDatabaseName("checkin_route_ix");
        builder.HasIndex(e => e.StartedAt).HasDatabaseName("checkin_started_ix");

        builder.HasOne<RouteEntity>().WithMany().HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RouteStopEntity>().WithMany().HasForeignKey(e => e.RouteStopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChildEntity>().WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckInEventEntityConfiguration : IEntityTypeConfiguration<CheckInEventEntity>
{
    public void Configure(EntityTypeBuilder<CheckInEventEntity> builder)
    {
        builder.ToTable("checkin_event");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CheckInId).HasColumnName("checkin_id");
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.CheckInId).HasDatabaseName("checkin_event_checkin_ix");

        builder.HasOne<CheckInEntity>().WithMany().HasForeignKey(e => e.CheckInId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FallbackPinEntityConfiguration : IEntityTypeConfiguration<FallbackPinEntity>
{
    public void Configure(EntityTypeBuilder<FallbackPinEntity> builder)
    {
        builder.ToTable("fallback_pin");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.PinHash).HasColumnName("pin_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.ValidFrom).HasColumnName("valid_from").IsRequired();
        builder.Property(e => e.ValidUntil).HasColumnName("valid_until").IsRequired();
        builder.Property(e => e.Attempts).HasColumnName("attempts").HasDefaultValue((short)0).IsRequired();
        builder.Property(e => e.ConsumedAt).HasColumnName("consumed_at");

        builder.HasIndex(e => new { e.ChildId, e.ValidUntil }).HasDatabaseName("fallback_pin_child_validity_ix");

        builder.HasOne<ChildEntity>().WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ActorIdHash).HasColumnName("actor_id_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").HasMaxLength(32).IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(e => e.TargetType).HasColumnName("target_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.TargetId).HasColumnName("target_id").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("NOW()");
        builder.Property(e => e.RetentionUntil).HasColumnName("retention_until").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Action }).HasDatabaseName("audit_log_tenant_action_ix");
        builder.HasIndex(e => e.RetentionUntil).HasDatabaseName("audit_log_retention_ix");
    }
}

public sealed class NotificationOutboxEntityConfiguration : IEntityTypeConfiguration<NotificationOutboxEntity>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxEntity> builder)
    {
        builder.ToTable("notification_outbox");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FamilyId).HasColumnName("family_id");
        builder.Property(e => e.ChildId).HasColumnName("child_id");
        builder.Property(e => e.CheckInId).HasColumnName("checkin_id");
        builder.Property(e => e.Template).HasColumnName("template").HasMaxLength(64).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(e => e.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).HasDefaultValue("pending").IsRequired();

        builder.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("notification_outbox_status_ix");
    }
}
