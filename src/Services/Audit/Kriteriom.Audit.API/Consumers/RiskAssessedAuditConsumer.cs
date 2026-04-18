using System.Text.Json;
using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;

namespace Kriteriom.Audit.API.Consumers;

/// <summary>
/// Consumes RiskAssessedIntegrationEvent and persists an immutable audit record.
/// </summary>
public class RiskAssessedAuditConsumer(
    IAuditRepository auditRepository,
    ILogger<RiskAssessedAuditConsumer> logger)
    : IConsumer<RiskAssessedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RiskAssessedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Audit: received RiskAssessedIntegrationEvent. EventId={EventId}, CreditId={CreditId}, Decision={Decision}, RiskScore={RiskScore}",
            message.EventId, message.CreditId, message.Decision, message.RiskScore);

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
            EventType = nameof(RiskAssessedIntegrationEvent),
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
            "Audit: record saved. AuditRecordId={AuditRecordId}, EventType={EventType}, CreditId={CreditId}, Decision={Decision}",
            record.Id, record.EventType, message.CreditId, message.Decision);
    }
}
