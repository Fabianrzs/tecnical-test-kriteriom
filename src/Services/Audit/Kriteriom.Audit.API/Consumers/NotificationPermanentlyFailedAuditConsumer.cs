using System.Text.Json;
using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;

namespace Kriteriom.Audit.API.Consumers;

public class NotificationPermanentlyFailedAuditConsumer(
    IAuditRepository auditRepository,
    ILogger<NotificationPermanentlyFailedAuditConsumer> logger)
    : IConsumer<NotificationPermanentlyFailedEvent>
{
    public async Task Consume(ConsumeContext<NotificationPermanentlyFailedEvent> context)
    {
        var message = context.Message;
        var ct      = context.CancellationToken;

        if (await auditRepository.ExistsAsync(message.EventId, ct))
        {
            logger.LogWarning(
                "Audit: duplicate NotificationPermanentlyFailedEvent skipped. EventId={EventId}",
                message.EventId);
            return;
        }

        var record = new AuditRecord
        {
            Id            = Guid.NewGuid(),
            EventType     = nameof(NotificationPermanentlyFailedEvent),
            EventId       = message.EventId,
            CorrelationId = message.CorrelationId,
            EntityId      = message.NotificationId,
            Payload       = JsonSerializer.Serialize(message),
            OccurredOn    = message.OccurredOn,
            RecordedAt    = DateTime.UtcNow,
            ServiceName   = "audit-service"
        };

        await auditRepository.AddAsync(record, ct);

        logger.LogWarning(
            "Audit: notification permanently failed. NotificationId={NotificationId}, CreditId={CreditId}, " +
            "EventType={EventType}, TotalAttempts={TotalAttempts}, LastError={LastError}",
            message.NotificationId, message.CreditId, message.EventType,
            message.TotalAttempts, message.LastError);
    }
}
