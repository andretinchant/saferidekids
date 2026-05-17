using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SafeRideKids.Biometria.Core.Audit;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Infrastructure.Audit;

/// <summary>
/// Implementação EF Core do audit logger. Retention_until = hoje + 6 anos (Marco Civil + ECA).
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365 * 6);

    private readonly BiometriaDbContext _db;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(BiometriaDbContext db, ILogger<AuditLogger> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogAsync(
        string tenantId,
        string actorIdHash,
        string actorType,
        string action,
        string targetType,
        string targetId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var entity = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorIdHash = actorIdHash,
            ActorType = actorType,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            OccurredAt = DateTimeOffset.UtcNow,
            RetentionUntil = DateOnly.FromDateTime(DateTime.UtcNow.Add(RetentionPeriod))
        };

        await _db.AuditLogs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Audit logged: tenant={Tenant} action={Action} targetType={TargetType}",
            tenantId, action, targetType);
    }
}
