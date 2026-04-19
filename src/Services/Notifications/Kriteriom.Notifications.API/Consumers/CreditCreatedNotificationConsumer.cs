using Kriteriom.Notifications.API.Metrics;
using Kriteriom.Notifications.Domain.Entities;
using Kriteriom.Notifications.Domain.Repositories;
using Kriteriom.Notifications.Domain.Services;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Kriteriom.Notifications.API.Consumers;

public class CreditCreatedNotificationConsumer(
    INotificationSender notificationSender,
    INotificationRepository notificationRepository,
    ILogger<CreditCreatedNotificationConsumer> logger)
    : IConsumer<CreditCreatedIntegrationEvent>
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt));

    private static readonly AsyncCircuitBreakerPolicy CircuitBreaker = Policy
        .Handle<Exception>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30));

    private static readonly IAsyncPolicy DeliveryPolicy = Policy.WrapAsync(RetryPolicy, CircuitBreaker);

    public async Task Consume(ConsumeContext<CreditCreatedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct      = context.CancellationToken;

        if (message.EventId == Guid.Empty)
        {
            logger.LogWarning("Skipping notification with empty EventId for CreditId={CreditId}", message.CreditId);
            return;
        }

        if (await notificationRepository.ExistsForEventAsync(message.EventId, ct))
        {
            logger.LogWarning(
                "Duplicate event skipped. EventId={EventId}, CreditId={CreditId}",
                message.EventId, message.CreditId);
            return;
        }

        const string subject = "Solicitud de Crédito Recibida";
        var body = $"Recibimos su solicitud de crédito {message.CreditId}. " +
                   $"Monto: {message.Amount:C2}. Estado: Pendiente de evaluación de riesgo. " +
                   $"Consulte su crédito en: http://portal.kriteriom.com/credits/{message.CreditId}";
        var recipient = $"client-{message.ClientId}";

        var notificationId = await notificationRepository.CreateAsync(new NotificationRecord
        {
            EventId   = message.EventId,
            CreditId  = message.CreditId,
            EventType = nameof(CreditCreatedIntegrationEvent),
            Recipient = recipient,
            Subject   = subject,
            Body      = body
        }, ct);

        logger.LogInformation(
            "Notification {NotificationId} created (Pending). CreditId={CreditId}",
            notificationId, message.CreditId);

        try
        {
            await DeliveryPolicy.ExecuteAsync(async () =>
                await notificationSender.SendAsync(recipient, subject, body, ct));

            await notificationRepository.MarkSentAsync(notificationId, ct);
            NotificationMetrics.SentTotal.WithLabels(nameof(CreditCreatedIntegrationEvent)).Inc();

            logger.LogInformation(
                "Notification {NotificationId} sent. CreditId={CreditId}",
                notificationId, message.CreditId);
        }
        catch (Exception ex)
        {
            await notificationRepository.MarkFailedAsync(notificationId, ex.Message, ct);
            NotificationMetrics.FailedTotal.WithLabels(nameof(CreditCreatedIntegrationEvent)).Inc();

            logger.LogWarning(ex,
                "Notification {NotificationId} failed. Publishing compensation event. CreditId={CreditId}",
                notificationId, message.CreditId);

            await context.Publish(new NotificationDeliveryFailedEvent
            {
                NotificationId = notificationId,
                CreditId       = message.CreditId,
                EventType      = nameof(CreditCreatedIntegrationEvent),
                Recipient      = recipient,
                Subject        = subject,
                Body           = body,
                AttemptCount   = 1,
                ErrorMessage   = ex.Message,
                CorrelationId  = context.CorrelationId?.ToString() ?? string.Empty
            }, ct);
        }
    }
}
