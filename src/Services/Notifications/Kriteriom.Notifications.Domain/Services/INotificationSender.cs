namespace Kriteriom.Notifications.Domain.Services;

public interface INotificationSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default);
}
