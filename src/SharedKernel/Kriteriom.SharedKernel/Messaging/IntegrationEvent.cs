namespace Kriteriom.SharedKernel.Messaging;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}
