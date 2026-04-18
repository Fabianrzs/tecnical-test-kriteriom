using System.Text.Json;
using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Audit.API.Consumers;

/// <summary>
/// Consumes CreditUpdatedIntegrationEvent and persists an immutable audit record.
/// </summary>
public class CreditUpdatedAuditConsumer(
    IAuditRepository auditRepository,
    ILogger<CreditUpdatedAuditConsumer> logger)
    : IConsumer<CreditUpdatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CreditUpdatedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Audit: received CreditUpdatedIntegrationEvent. EventId={EventId}, CreditId={CreditId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
            message.EventId, message.CreditId, message.OldStatus, message.NewStatus);

        // Idempotency check via repository before processing
        if (await auditRepository.ExistsAsync(message.EventId, ct))
        {
            logger.LogWarning(
                "Audit: duplicate event skipped. EventId={EventId}, CreditId={CreditId}",
                message.EventId, message.CreditId);
            return;
        }

        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            EventType = nameof(CreditUpdatedIntegrationEvent),
            EventId = message.EventId,
            CorrelationId = message.CorrelationId,
            EntityId = message.CreditId,
            Payload = JsonSerializer.Serialize(message),
            OccurredOn = message.OccurredOn,
            RecordedAt = DateTime.UtcNow,
            ServiceName = "audit-service"
        };

        await auditRepository.AddAsync(record, ct);

        logger.LogInformation(
            "Audit: record saved. AuditRecordId={AuditRecordId}, EventType={EventType}, CreditId={CreditId}, StatusChange={OldStatus}->{NewStatus}",
            record.Id, record.EventType, message.CreditId, message.OldStatus, message.NewStatus);
    }
}
