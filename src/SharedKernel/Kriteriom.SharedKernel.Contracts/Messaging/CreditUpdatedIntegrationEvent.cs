namespace Kriteriom.SharedKernel.Messaging;

public record CreditUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid CreditId { get; init; }
    public string OldStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}
