using Microsoft.EntityFrameworkCore;
using SafeRideKids.Biometria.Infrastructure.Persistence.Configurations;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Infrastructure.Persistence;

/// <summary>
/// DbContext principal da POC de biometria. Schema dedicado 'biometria_poc'.
/// </summary>
public sealed class BiometriaDbContext : DbContext
{
    public const string SchemaName = "biometria_poc";

    public BiometriaDbContext(DbContextOptions<BiometriaDbContext> options) : base(options)
    {
    }

    public DbSet<FamilyEntity> Families => Set<FamilyEntity>();
    public DbSet<ChildEntity> Children => Set<ChildEntity>();
    public DbSet<ConsentEntity> Consents => Set<ConsentEntity>();
    public DbSet<EnrollmentEntity> Enrollments => Set<EnrollmentEntity>();
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();
    public DbSet<RouteStopEntity> RouteStops => Set<RouteStopEntity>();
    public DbSet<CheckInEntity> CheckIns => Set<CheckInEntity>();
    public DbSet<CheckInEventEntity> CheckInEvents => Set<CheckInEventEntity>();
    public DbSet<FallbackPinEntity> FallbackPins => Set<FallbackPinEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<NotificationOutboxEntity> NotificationOutbox => Set<NotificationOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.ApplyConfiguration(new FamilyEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChildEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ConsentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EnrollmentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RouteEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RouteStopEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CheckInEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CheckInEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new FallbackPinEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationOutboxEntityConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
