using Kriteriom.Notifications.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Notifications.Infrastructure.Services;

public class LogNotificationSender(
    ILogger<LogNotificationSender> logger,
    IConfiguration configuration) : INotificationSender
{
    private static readonly Random Random = new();
    private readonly bool _simulateFailures =
        string.Equals(configuration["FeatureFlags:SimulateFailures"], "true", StringComparison.OrdinalIgnoreCase);

    public Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
    {
        if (_simulateFailures && Random.NextDouble() < 0.10)
        {
            logger.LogWarning("Notification delivery failure (simulated). Recipient={Recipient}", recipient);
            throw new InvalidOperationException($"Simulated delivery failure for '{recipient}'");
        }

        logger.LogInformation(
            "NOTIFICATION SENT [simulated] | Recipient={Recipient} | Subject={Subject} | Body={Body}",
            recipient, subject, body);

        return Task.CompletedTask;
    }
}
