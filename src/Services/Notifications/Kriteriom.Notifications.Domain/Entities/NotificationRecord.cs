namespace Kriteriom.Notifications.Domain.Entities;

public class NotificationRecord
{
    public Guid Id            { get; set; }
    public Guid? EventId      { get; set; } // integration event origin — used for idempotency
    public Guid CreditId      { get; set; }
    public string EventType   { get; set; } = string.Empty;
    public string Recipient   { get; set; } = string.Empty;
    public string Subject     { get; set; } = string.Empty;
    public string Body        { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount     { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt   { get; set; }
}

public enum NotificationStatus
{
    Pending = 0,
    Sent    = 1,
    Failed  = 2
}
