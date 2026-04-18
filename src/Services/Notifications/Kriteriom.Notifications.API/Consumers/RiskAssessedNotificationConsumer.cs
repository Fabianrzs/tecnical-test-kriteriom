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

public class RiskAssessedNotificationConsumer(
    INotificationSender notificationSender,
    INotificationRepository notificationRepository,
    ILogger<RiskAssessedNotificationConsumer> logger)
    : IConsumer<RiskAssessedIntegrationEvent>
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

    public async Task Consume(ConsumeContext<RiskAssessedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct      = context.CancellationToken;

        if (await notificationRepository.ExistsForEventAsync(message.EventId, ct))
        {
            logger.LogWarning(
                "Duplicate event skipped. EventId={EventId}, CreditId={CreditId}",
                message.EventId, message.CreditId);
            return;
        }

        var (subject, body) = message.Decision switch
        {
            "Approved" => (
                "¡Crédito Aprobado!",
                $"Su solicitud de crédito {message.CreditId} fue aprobada. " +
                $"Score de riesgo: {message.RiskScore:F1}/100. {message.Reason}"),
            "Rejected" => (
                "Solicitud de Crédito Rechazada",
                $"Su solicitud de crédito {message.CreditId} fue rechazada. " +
                $"Motivo: {message.Reason}. Score de riesgo: {message.RiskScore:F1}/100."),
            "UnderReview" => (
                "Solicitud en Revisión Manual",
                $"Su solicitud de crédito {message.CreditId} requiere revisión adicional. " +
                $"Motivo: {message.Reason}. Le notificaremos a la brevedad."),
            _ => (
                "Actualización de Solicitud",
                $"Su solicitud {message.CreditId} ha sido actualizada. Estado: {message.Decision}")
        };

        var recipient = $"credit-{message.CreditId}";

        var notificationId = await notificationRepository.CreateAsync(new NotificationRecord
        {
            EventId   = message.EventId,
            CreditId  = message.CreditId,
            EventType = nameof(RiskAssessedIntegrationEvent),
            Recipient = recipient,
            Subject   = subject,
            Body      = body
        }, ct);

        logger.LogInformation(
            "Notification {NotificationId} created (Pending). CreditId={CreditId}, Decision={Decision}",
            notificationId, message.CreditId, message.Decision);

        try
        {
            await DeliveryPolicy.ExecuteAsync(async () =>
                await notificationSender.SendAsync(recipient, subject, body, ct));

            await notificationRepository.MarkSentAsync(notificationId, ct);
            NotificationMetrics.SentTotal.WithLabels(nameof(RiskAssessedIntegrationEvent)).Inc();

            logger.LogInformation(
                "Notification {NotificationId} sent. CreditId={CreditId}, Decision={Decision}",
                notificationId, message.CreditId, message.Decision);
        }
        catch (Exception ex)
        {
            await notificationRepository.MarkFailedAsync(notificationId, ex.Message, ct);
            NotificationMetrics.FailedTotal.WithLabels(nameof(RiskAssessedIntegrationEvent)).Inc();

            logger.LogWarning(ex,
                "Notification {NotificationId} failed. Publishing compensation event. CreditId={CreditId}",
                notificationId, message.CreditId);

            await context.Publish(new NotificationDeliveryFailedEvent
            {
                NotificationId = notificationId,
                CreditId       = message.CreditId,
                EventType      = nameof(RiskAssessedIntegrationEvent),
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
