using System.Text.Json;
using Kriteriom.SharedKernel.Messaging;
using Kriteriom.SharedKernel.Outbox;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace Kriteriom.Credits.Infrastructure.Messaging;

public class OutboxProcessorService(
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<OutboxProcessorService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxProcessorService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in OutboxProcessorService main loop");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("OutboxProcessorService stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var pendingMessages = (await outboxRepo.GetPendingAsync(50, ct)).ToList();

        if (pendingMessages.Count == 0)
            return;

        logger.LogInformation("Processing {Count} pending outbox messages", pendingMessages.Count);

        var pipeline = pipelineProvider.GetPipeline("outbox-retry");

        foreach (var message in pendingMessages)
        {
            if (ct.IsCancellationRequested) break;

            await ProcessMessageWithRetryAsync(message, bus, outboxRepo, pipeline, ct);
        }
    }

    private async Task ProcessMessageWithRetryAsync(
        OutboxMessage message,
        IBus bus,
        IOutboxRepository outboxRepo,
        ResiliencePipeline pipeline,
        CancellationToken ct)
    {
        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                if (Random.Shared.NextDouble() < 0.05)
                    throw new InvalidOperationException("SIMULATED: Intermittent failure");

                await PublishMessageAsync(message, bus, token);
                await outboxRepo.MarkProcessedAsync(message.Id, token);

                logger.LogInformation(
                    "Outbox message {MessageId} of type {EventType} published successfully",
                    message.Id, message.EventType);
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process outbox message {MessageId} of type {EventType} after retries. RetryCount={RetryCount}",
                message.Id, message.EventType, message.RetryCount + 1);

            await outboxRepo.MarkFailedAsync(message.Id, ex.Message, ct);
        }
    }

    private async Task PublishMessageAsync(OutboxMessage message, IBus bus, CancellationToken ct)
    {
        switch (message.EventType)
        {
            case nameof(CreditCreatedIntegrationEvent):
            {
                var evt = JsonSerializer.Deserialize<CreditCreatedIntegrationEvent>(message.Payload, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize {message.EventType}");
                await bus.Publish(evt, ct);
                break;
            }
            case nameof(CreditUpdatedIntegrationEvent):
            {
                var evt = JsonSerializer.Deserialize<CreditUpdatedIntegrationEvent>(message.Payload, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize {message.EventType}");
                await bus.Publish(evt, ct);
                break;
            }
            case nameof(RiskAssessedIntegrationEvent):
            {
                var evt = JsonSerializer.Deserialize<RiskAssessedIntegrationEvent>(message.Payload, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize {message.EventType}");
                await bus.Publish(evt, ct);
                break;
            }
            default:
                logger.LogWarning(
                    "Unknown event type {EventType} for outbox message {MessageId}. Skipping.",
                    message.EventType, message.Id);
                break;
        }
    }
}
