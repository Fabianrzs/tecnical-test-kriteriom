using System.Text.Json;
using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Audit.API.Consumers;

public class ClientCreatedAuditConsumer(
    IAuditRepository auditRepository,
    ILogger<ClientCreatedAuditConsumer> logger)
    : IConsumer<ClientCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ClientCreatedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Audit: received ClientCreatedIntegrationEvent. EventId={EventId}, ClientId={ClientId}",
            message.EventId, message.ClientId);

        if (await auditRepository.ExistsAsync(message.EventId, ct))
        {
            logger.LogWarning("Audit: duplicate event skipped. EventId={EventId}", message.EventId);
            return;
        }

        var record = new AuditRecord
        {
            Id            = Guid.NewGuid(),
            EventType     = nameof(ClientCreatedIntegrationEvent),
            EventId       = message.EventId,
            CorrelationId = message.CorrelationId,
            EntityId      = message.ClientId,
            Payload       = JsonSerializer.Serialize(message),
            OccurredOn    = message.OccurredOn,
            RecordedAt    = DateTime.UtcNow,
            ServiceName   = "audit-service"
        };

        await auditRepository.AddAsync(record, ct);

        logger.LogInformation(
            "Audit: client creation recorded. AuditRecordId={AuditRecordId}, ClientId={ClientId}",
            record.Id, message.ClientId);
    }
}
