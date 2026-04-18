namespace Kriteriom.SharedKernel.Messaging;

public record NotificationDeliveryFailedEvent : IntegrationEvent
{
    public Guid   NotificationId { get; init; }
    public Guid   CreditId       { get; init; }
    public string EventType      { get; init; } = string.Empty;
    public string Recipient      { get; init; } = string.Empty;
    public string Subject        { get; init; } = string.Empty;
    public string Body           { get; init; } = string.Empty;
    public int    AttemptCount   { get; init; }
    public string ErrorMessage   { get; init; } = string.Empty;
}
