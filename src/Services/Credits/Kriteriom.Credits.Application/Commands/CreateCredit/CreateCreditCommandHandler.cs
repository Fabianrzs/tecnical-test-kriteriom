using System.Text.Json;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.Contracts.Idempotency;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.CreateCredit;

public class CreateCreditCommandHandler(
    ICreditRepository creditRepository,
    IClientRepository clientRepository,
    IIdempotencyService idempotencyService,
    ILogger<CreateCreditCommandHandler> logger) : IRequestHandler<CreateCreditCommand, Result<CreditDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<Result<CreditDto>> Handle(CreateCreditCommand request, CancellationToken ct)
    {
        var cachedJson = await idempotencyService.GetAsync(request.IdempotencyKey, ct);
        if (cachedJson is not null)
        {
            logger.LogInformation("Idempotency hit for key {Key}", request.IdempotencyKey);
            var cachedDto = JsonSerializer.Deserialize<CreditDto>(cachedJson, JsonOptions);
            if (cachedDto is not null) return Result<CreditDto>.Success(cachedDto);
        }

        var client = await clientRepository.GetByIdAsync(request.ClientId, ct);
        if (client is null)
            return Result<CreditDto>.Failure($"Cliente {request.ClientId} no encontrado", "CLIENT_NOT_FOUND");

        Credit? credit = null;

        try
        {
            var activeCredits        = await creditRepository.GetActiveCreditsForClientAsync(request.ClientId, ct);
            var existingMonthlyDebt  = activeCredits.Sum(c => c.MonthlyPayment());
            var newMonthlyPayment    = Credit.ComputeMonthlyPayment(request.Amount, request.InterestRate, request.TermMonths);
            var projectedDti         = (existingMonthlyDebt + newMonthlyPayment) / client.MonthlyIncome * 100m;

            if (projectedDti > 60m)
                throw new DebtCapacityExceededException(projectedDti);

            credit = Credit.Create(
                request.ClientId, request.Amount, request.InterestRate, request.TermMonths,
                monthlyIncome:       client.MonthlyIncome,
                existingMonthlyDebt: existingMonthlyDebt,
                clientCreditScore:   client.CreditScore);

            await creditRepository.AddAsync(credit, ct);
        }
        catch (DebtCapacityExceededException ex)
        {
            return Result<CreditDto>.Failure(ex.Message, "DEBT_CAPACITY_EXCEEDED");
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

        var creditDto = credit!.ToDto();

        // Cache only after the transaction commits to avoid stale idempotency responses.
        await idempotencyService.SetAsync(
            request.IdempotencyKey,
            JsonSerializer.Serialize(creditDto, JsonOptions),
            null, ct);

        logger.LogInformation(
            "Credit {CreditId} created for client {ClientId} (income={Income}, score={Score})",
            credit!.Id, credit.ClientId, client.MonthlyIncome, client.CreditScore);

        return Result<CreditDto>.Success(creditDto);
    }
}
