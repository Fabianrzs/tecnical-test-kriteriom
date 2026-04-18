namespace Kriteriom.Audit.Domain.Entities;

public class AuditRecord
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public DateTime RecordedAt { get; set; }
    public string ServiceName { get; set; } = "audit-service";
}
