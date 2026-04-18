namespace Kriteriom.SharedKernel.Messaging;

public record RecalculateCreditStatusesRequestedEvent : IntegrationEvent
{
    public int BatchSize { get; init; } = 500;
}
