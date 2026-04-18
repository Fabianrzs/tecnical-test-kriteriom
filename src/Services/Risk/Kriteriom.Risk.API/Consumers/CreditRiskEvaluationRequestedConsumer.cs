using Kriteriom.Risk.Domain.Domain;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;

namespace Kriteriom.Risk.API.Consumers;

public class CreditRiskEvaluationRequestedConsumer(
    ILogger<CreditRiskEvaluationRequestedConsumer> logger,
    IHttpClientFactory httpClientFactory,
    IBus bus,
    IDistributedCache cache) : IConsumer<CreditRiskEvaluationRequestedEvent>
{
    private const string KeyPrefix = "risk:processed:";

    public async Task Consume(ConsumeContext<CreditRiskEvaluationRequestedEvent> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation(
            "CreditRiskEvaluationRequested received. EventId={EventId} CreditId={CreditId}",
            msg.EventId, msg.CreditId);

        var idempotencyKey = $"{KeyPrefix}{msg.EventId}";
        if (await cache.GetStringAsync(idempotencyKey, ct) is not null)
        {
            logger.LogWarning("Duplicate event skipped. EventId={EventId}", msg.EventId);
            return;
        }

        var assessment = await RiskCalculator.AssessAsync(
            creditId:            msg.CreditId,
            amount:              msg.Amount,
            annualInterestRate:  msg.InterestRate,
            termMonths:          msg.TermMonths,
            monthlyIncome:       msg.MonthlyIncome,
            existingMonthlyDebt: msg.ExistingMonthlyDebt,
            creditScore:         msg.ClientCreditScore,
            ct:                  ct);

        logger.LogInformation(
            "Risk assessed (batch). CreditId={CreditId} Score={Score} Decision={Decision}",
            assessment.CreditId, assessment.RiskScore, assessment.Decision);

        await CallCreditsApiAsync(assessment, ct);

        await bus.Publish(new RiskAssessedIntegrationEvent
        {
            CreditId      = assessment.CreditId,
            RiskScore     = assessment.RiskScore,
            Decision      = assessment.Decision,
            Reason        = assessment.Reason,
            CorrelationId = msg.CorrelationId
        }, ct);

        await cache.SetStringAsync(
            idempotencyKey,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
            ct);

        logger.LogInformation("RiskAssessedEvent published (batch). CreditId={CreditId}", msg.CreditId);
    }

    private async Task CallCreditsApiAsync(RiskAssessmentResult assessment, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("credits-api");
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
