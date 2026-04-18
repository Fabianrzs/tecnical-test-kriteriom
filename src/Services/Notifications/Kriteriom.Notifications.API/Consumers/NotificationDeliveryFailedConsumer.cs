using Kriteriom.Notifications.API.Metrics;
using Kriteriom.Notifications.Domain.Repositories;
using Kriteriom.Notifications.Domain.Services;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;

namespace Kriteriom.Notifications.API.Consumers;

public class NotificationDeliveryFailedConsumer(
    INotificationSender sender,
    INotificationRepository repository,
    ILogger<NotificationDeliveryFailedConsumer> logger,
    IConfiguration configuration)
    : IConsumer<NotificationDeliveryFailedEvent>
{
    private int MaxCompensationRetries =>
        int.TryParse(configuration["Notifications:MaxCompensationRetries"], out var v) ? v : 5;

    public async Task Consume(ConsumeContext<NotificationDeliveryFailedEvent> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        if (msg.AttemptCount >= MaxCompensationRetries)
        {
            await repository.MarkFailedAsync(
                msg.NotificationId,
                $"Permanently failed after {msg.AttemptCount} attempts: {msg.ErrorMessage}",
                ct);

            NotificationMetrics.PermanentlyFailedTotal.Inc();

            logger.LogError(
                "Notification {Id} permanently failed after {Max} attempts. CreditId={CreditId}, LastError={Error}",
                msg.NotificationId, MaxCompensationRetries, msg.CreditId, msg.ErrorMessage);

            await context.Publish(new NotificationPermanentlyFailedEvent
            {
                NotificationId = msg.NotificationId,
                CreditId       = msg.CreditId,
                EventType      = msg.EventType,
                Recipient      = msg.Recipient,
                TotalAttempts  = msg.AttemptCount,
                LastError      = msg.ErrorMessage,
                CorrelationId  = msg.CorrelationId
            }, ct);

            return;
        }

        logger.LogInformation(
            "Compensation attempt {N}/{Max} for notification {Id}. CreditId={CreditId}",
            msg.AttemptCount + 1, MaxCompensationRetries, msg.NotificationId, msg.CreditId);

        try
        {
            await sender.SendAsync(msg.Recipient, msg.Subject, msg.Body, ct);
            await repository.MarkSentAsync(msg.NotificationId, ct);

            NotificationMetrics.CompensatedTotal.WithLabels("sent").Inc();

            logger.LogInformation(
                "Notification {Id} delivered on compensation attempt {N}",
                msg.NotificationId, msg.AttemptCount + 1);
        }
        catch (Exception ex)
        {
            await repository.MarkFailedAsync(msg.NotificationId, ex.Message, ct);
            NotificationMetrics.CompensatedTotal.WithLabels("failed").Inc();

            logger.LogWarning(
                "Notification {Id} compensation attempt {N} failed: {Error}. Scheduling attempt {Next}.",
                msg.NotificationId, msg.AttemptCount + 1, ex.Message, msg.AttemptCount + 2);

            await context.Publish(new NotificationDeliveryFailedEvent
            {
                NotificationId = msg.NotificationId,
                CreditId       = msg.CreditId,
                EventType      = msg.EventType,
                Recipient      = msg.Recipient,
                Subject        = msg.Subject,
                Body           = msg.Body,
                AttemptCount   = msg.AttemptCount + 1,
                ErrorMessage   = ex.Message,
                CorrelationId  = msg.CorrelationId
            }, ct);
        }
    }
}
