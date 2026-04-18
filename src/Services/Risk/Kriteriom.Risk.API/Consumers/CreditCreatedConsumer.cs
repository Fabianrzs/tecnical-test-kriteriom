using Kriteriom.Risk.Domain.Domain;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;

namespace Kriteriom.Risk.API.Consumers;

public class CreditCreatedConsumer(
    ILogger<CreditCreatedConsumer> logger,
    IHttpClientFactory httpClientFactory,
    IBus bus,
    IDistributedCache cache,
    IConfiguration config) : IConsumer<CreditCreatedIntegrationEvent>
{
    private const string KeyPrefix = "risk:processed:";

    public async Task Consume(ConsumeContext<CreditCreatedIntegrationEvent> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation(
            "CreditCreatedEvent received. EventId={EventId} CreditId={CreditId} ClientId={ClientId}",
            msg.EventId, msg.CreditId, msg.ClientId);

        var idempotencyKey = $"{KeyPrefix}{msg.EventId}";
        if (await cache.GetStringAsync(idempotencyKey, ct) is not null)
        {
            logger.LogWarning("Duplicate event skipped. EventId={EventId}", msg.EventId);
            return;
        }

        var simulateLatency = config.GetValue<bool>("FeatureFlags:SimulateLatency");
        var latencyMs       = config.GetValue<int>("FeatureFlags:LatencyMs");
        if (simulateLatency && latencyMs > 0)
            await Task.Delay(latencyMs, ct);

        var assessment = await RiskCalculator.AssessAsync(
            creditId:           msg.CreditId,
            amount:             msg.Amount,
            annualInterestRate: msg.InterestRate,
            termMonths:         msg.TermMonths,
            monthlyIncome:      msg.MonthlyIncome,
            existingMonthlyDebt: msg.ExistingMonthlyDebt,
            creditScore:        msg.ClientCreditScore,
            ct:                 ct);

        logger.LogInformation(
            "Risk assessed. CreditId={CreditId} DTI-driven Score={Score} Decision={Decision} Reason={Reason}",
            assessment.CreditId, assessment.RiskScore, assessment.Decision, assessment.Reason);

        await CallCreditsApiAsync(assessment, ct);

        var riskEvent = new RiskAssessedIntegrationEvent
        {
            CreditId      = assessment.CreditId,
            RiskScore     = assessment.RiskScore,
            Decision      = assessment.Decision,
            Reason        = assessment.Reason,
            CorrelationId = msg.CorrelationId
        };
        await bus.Publish(riskEvent, ct);

        await cache.SetStringAsync(
            idempotencyKey,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
            ct);

        logger.LogInformation("RiskAssessedEvent published. CreditId={CreditId}", msg.CreditId);
    }

    private async Task CallCreditsApiAsync(RiskAssessmentResult assessment, CancellationToken ct)
    {
        var http    = httpClientFactory.CreateClient("credits-api");
        var payload = new
        {
            riskScore = assessment.RiskScore,
            decision  = assessment.Decision,
            reason    = assessment.Reason
        };

        var response = await http.PostAsJsonAsync(
            $"/api/credits/{assessment.CreditId}/risk", payload, ct);

        if (!response.IsSuccessStatusCode)
            logger.LogWarning(
                "Credits API returned {Status} for CreditId={CreditId}",
                (int)response.StatusCode, assessment.CreditId);
    }
}
