using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SafeRideKids.Biometria.Core.Notifications;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Infrastructure.Notifications;

/// <summary>
/// Persiste notificações em 'biometria_poc.notification_outbox'.
/// Worker externo (fora do escopo da POC) consumirá esses registros
/// e despachará via canal real (FCM/SES). Por enquanto, dashboard inspeciona.
/// </summary>
public sealed class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly BiometriaDbContext _db;
    private readonly ILogger<NotificationOutboxRepository> _logger;

    public NotificationOutboxRepository(BiometriaDbContext db, ILogger<NotificationOutboxRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new NotificationOutboxEntity
        {
            Id = Guid.NewGuid(),
            TenantId = message.TenantId,
            FamilyId = message.FamilyId,
            ChildId = message.ChildId,
            CheckInId = message.CheckInId,
            Template = message.Template.ToString(),
            PayloadJson = message.PayloadJson,
            CreatedAt = message.CreatedAt,
            Status = "pending"
        };

        await _db.NotificationOutbox.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Loga apenas IDs/templates; payload pode conter nome de criança (clear-text decifrado),
        // por isso fica apenas no DB (que tem controle de acesso por tenant).
        _logger.LogInformation(
            "Notification enqueued: id={Id} tenant={Tenant} template={Template}",
            entity.Id, entity.TenantId, entity.Template);
    }
}
