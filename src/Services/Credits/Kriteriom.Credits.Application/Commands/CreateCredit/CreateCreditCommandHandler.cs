using System.Text.Json;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Application.Services;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.Messaging;
using Kriteriom.SharedKernel.Outbox;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.CreateCredit;

public class CreateCreditCommandHandler(
    ICreditRepository creditRepository,
    IClientRepository clientRepository,
    IOutboxRepository outboxRepository,
    IIdempotencyService idempotencyService,
    IUnitOfWork unitOfWork,
    ILogger<CreateCreditCommandHandler> logger) : IRequestHandler<CreateCreditCommand, Result<CreditDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<Result<CreditDto>> Handle(CreateCreditCommand request, CancellationToken ct)
    {
        var cachedResponse = await idempotencyService.GetAsync(request.IdempotencyKey, ct);
        if (cachedResponse is not null)
        {
            logger.LogInformation("Idempotency hit for key {Key}", request.IdempotencyKey);
            var cachedDto = JsonSerializer.Deserialize<CreditDto>(cachedResponse, JsonOptions);
            if (cachedDto is not null) return Result<CreditDto>.Success(cachedDto);
        }

        var client = await clientRepository.GetByIdAsync(request.ClientId, ct);
        if (client is null)
            return Result<CreditDto>.Failure($"Cliente {request.ClientId} no encontrado", "CLIENT_NOT_FOUND");

        var activeCredits = await creditRepository.GetActiveCreditsForClientAsync(request.ClientId, ct);
        var activeList = activeCredits.ToList();

        var existingMonthlyDebt = activeList.Sum(c => c.MonthlyPayment());
        var newMonthlyPayment   = Credit.ComputeMonthlyPayment(request.Amount, request.InterestRate, request.TermMonths);
        var projectedDti        = (existingMonthlyDebt + newMonthlyPayment) / client.MonthlyIncome * 100m;

        if (projectedDti > 60m)
            return Result<CreditDto>.Failure(
                $"La suma de pagos mensuales proyectada ({projectedDti:F1}%) supera el límite del 60% de los ingresos.",
                "DEBT_CAPACITY_EXCEEDED");

        try
        {
            var credit = Credit.Create(request.ClientId, request.Amount, request.InterestRate, request.TermMonths);

            var integrationEvent = new CreditCreatedIntegrationEvent
            {
                CreditId          = credit.Id,
                ClientId          = credit.ClientId,
                Amount            = credit.Amount,
                Status            = credit.Status.ToString(),
                InterestRate         = credit.InterestRate,
                TermMonths           = credit.TermMonths,
                MonthlyIncome        = client.MonthlyIncome,
                ExistingMonthlyDebt  = existingMonthlyDebt,
                ClientCreditScore = client.CreditScore,
                CorrelationId     = request.IdempotencyKey
            };

            var outboxMessage = new OutboxMessage
            {
                Id         = Guid.NewGuid(),
                EventType  = nameof(CreditCreatedIntegrationEvent),
                Payload    = JsonSerializer.Serialize(integrationEvent, JsonOptions),
                CreatedAt  = DateTime.UtcNow,
                RetryCount = 0
            };

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await creditRepository.AddAsync(credit, ct);
                await outboxRepository.AddAsync(outboxMessage, ct);
            }, ct);

            var creditDto = credit.ToDto();
            await idempotencyService.SetAsync(
                request.IdempotencyKey,
                JsonSerializer.Serialize(creditDto, JsonOptions),
                null, ct);

            logger.LogInformation(
                "Credit {CreditId} created for client {ClientId} (income={Income}, score={Score})",
                credit.Id, credit.ClientId, client.MonthlyIncome, client.CreditScore);

            return Result<CreditDto>.Success(creditDto);
        }
        catch (InvalidCreditOperationException ex)
        {
            logger.LogWarning(ex, "Invalid credit operation for client {ClientId}", request.ClientId);
            return Result<CreditDto>.Failure(ex.Message, "INVALID_CREDIT_OPERATION");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating credit for client {ClientId}", request.ClientId);
            return Result<CreditDto>.Failure("An unexpected error occurred while creating the credit", "INTERNAL_ERROR");
        }
    }
}
