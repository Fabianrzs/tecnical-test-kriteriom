using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.UpdateCreditStatus;

public class UpdateCreditStatusCommandHandler(
    ICreditRepository creditRepository,
    ILogger<UpdateCreditStatusCommandHandler> logger)
    : IRequestHandler<UpdateCreditStatusCommand, Result<CreditDto>>
{
    public async Task<Result<CreditDto>> Handle(UpdateCreditStatusCommand request, CancellationToken cancellationToken)
    {
        var credit = await creditRepository.GetByIdAsync(request.CreditId, cancellationToken);
        if (credit is null)
        {
            logger.LogWarning("Credit {CreditId} not found for status update", request.CreditId);
            return Result<CreditDto>.Failure($"Credit {request.CreditId} not found", "CREDIT_NOT_FOUND");
        }

        try
        {
            credit.UpdateStatus(request.NewStatus, request.Reason);
            await creditRepository.UpdateAsync(credit, cancellationToken);

            logger.LogInformation(
                "Credit {CreditId} status updated to {NewStatus}",
                credit.Id, credit.Status);

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
