using System.Text.Json;
using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Audit.API.Consumers;

/// <summary>
/// Consumes CreditCreatedIntegrationEvent and persists an immutable audit record.
/// </summary>
public class CreditCreatedAuditConsumer(
    IAuditRepository auditRepository,
    ILogger<CreditCreatedAuditConsumer> logger)
    : IConsumer<CreditCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CreditCreatedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Audit: received CreditCreatedIntegrationEvent. EventId={EventId}, CreditId={CreditId}, ClientId={ClientId}, Amount={Amount}",
            message.EventId, message.CreditId, message.ClientId, message.Amount);

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
            EventType = nameof(CreditCreatedIntegrationEvent),
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
            "Audit: record saved. AuditRecordId={AuditRecordId}, EventType={EventType}, CreditId={CreditId}",
            record.Id, record.EventType, message.CreditId);
    }
}
