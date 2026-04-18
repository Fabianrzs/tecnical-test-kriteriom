namespace Kriteriom.SharedKernel.Messaging;

public record NotificationPermanentlyFailedEvent : IntegrationEvent
{
    public Guid   NotificationId { get; init; }
    public Guid   CreditId       { get; init; }
    public string EventType      { get; init; } = string.Empty;
    public string Recipient      { get; init; } = string.Empty;
    public int    TotalAttempts  { get; init; }
    public string LastError      { get; init; } = string.Empty;
}
