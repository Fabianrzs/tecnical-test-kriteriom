using System.Text.Json;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Application.Services;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.Messaging;
using Kriteriom.SharedKernel.Outbox;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.UpdateCreditStatus;

public class UpdateCreditStatusCommandHandler(
    ICreditRepository creditRepository,
    IOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateCreditStatusCommandHandler> logger)
    : IRequestHandler<UpdateCreditStatusCommand, Result<CreditDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<Result<CreditDto>> Handle(UpdateCreditStatusCommand request, CancellationToken cancellationToken)
    {
        var credit = await creditRepository.GetByIdAsync(request.CreditId, cancellationToken);
        if (credit is null)
        {
            logger.LogWarning("Credit {CreditId} not found for status update", request.CreditId);
            return Result<CreditDto>.Failure($"Credit {request.CreditId} not found", "CREDIT_NOT_FOUND");
        }

        var oldStatus = credit.Status;

        try
        {
            credit.UpdateStatus(request.NewStatus, request.Reason);

            var integrationEvent = new CreditUpdatedIntegrationEvent
            {
                CreditId  = credit.Id,
                OldStatus = oldStatus.ToString(),
                NewStatus = credit.Status.ToString(),
                UpdatedAt = credit.UpdatedAt
            };

            var outboxMessage = new OutboxMessage
            {
                Id        = Guid.NewGuid(),
                EventType = nameof(CreditUpdatedIntegrationEvent),
                Payload   = JsonSerializer.Serialize(integrationEvent, JsonOptions),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await creditRepository.UpdateAsync(credit, cancellationToken);
                await outboxRepository.AddAsync(outboxMessage, cancellationToken);
            }, cancellationToken);

            logger.LogInformation(
                "Credit {CreditId} status updated from {OldStatus} to {NewStatus}",
                credit.Id, oldStatus, credit.Status);

            return Result<CreditDto>.Success(credit.ToDto());
        }
        catch (InvalidCreditOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation updating credit {CreditId} status", request.CreditId);
            return Result<CreditDto>.Failure(ex.Message, "INVALID_CREDIT_OPERATION");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating credit {CreditId} status", request.CreditId);
            return Result<CreditDto>.Failure("An unexpected error occurred", "INTERNAL_ERROR");
        }
    }
}
