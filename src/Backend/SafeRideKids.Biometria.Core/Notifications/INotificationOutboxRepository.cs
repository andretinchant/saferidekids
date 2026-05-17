using System;
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Notifications;

/// <summary>
/// Outbox de notificações ao responsável. Persiste mensagens em tabela
/// 'notification_outbox' (canal mock na POC). Pickup futuro por worker
/// que despacha para FCM/SES.
/// </summary>
public interface INotificationOutboxRepository
{
    Task EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken);
}

/// <summary>Mensagem enfileirada para o canal de notificação.</summary>
public sealed record NotificationMessage(
    string TenantId,
    Guid FamilyId,
    Guid ChildId,
    Guid? CheckInId,
    NotificationTemplate Template,
    string PayloadJson,                  // dados livres por template
    DateTimeOffset CreatedAt);

/// <summary>Templates de notificação previstos no contrato (Seção 9).</summary>
public enum NotificationTemplate
{
    CheckInApproved,
    CheckInFallbackUsed,
    CheckInRejectedAttention,
    EnrollmentExpiringSoon
}
