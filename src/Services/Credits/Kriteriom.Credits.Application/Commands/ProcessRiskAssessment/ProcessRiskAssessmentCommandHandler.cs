using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.ProcessRiskAssessment;

public class ProcessRiskAssessmentCommandHandler(
    ICreditRepository creditRepository,
    IClientRepository clientRepository,
    ILogger<ProcessRiskAssessmentCommandHandler> logger)
    : IRequestHandler<ProcessRiskAssessmentCommand, Result<CreditDto>>
{
    public async Task<Result<CreditDto>> Handle(ProcessRiskAssessmentCommand request, CancellationToken cancellationToken)
    {
        var credit = await creditRepository.GetByIdAsync(request.CreditId, cancellationToken);
        if (credit is null)
        {
            logger.LogWarning("Credit {CreditId} not found for risk assessment", request.CreditId);
            return Result<CreditDto>.Failure($"Credit {request.CreditId} not found", "CREDIT_NOT_FOUND");
        }

        if (credit is { RiskScore: not null, Status: not CreditStatus.Pending and not CreditStatus.UnderReview })
        {
            logger.LogInformation(
                "Risk assessment already applied for credit {CreditId} (Status={Status}). Skipping.",
                credit.Id, credit.Status);
            return Result<CreditDto>.Success(credit.ToDto());
        }

        if (!Enum.TryParse<RiskDecision>(request.Decision, ignoreCase: true, out var decision))
        {
            logger.LogWarning("Unknown risk decision '{Decision}' for credit {CreditId}", request.Decision, request.CreditId);
            return Result<CreditDto>.Failure($"Decisión de riesgo desconocida: '{request.Decision}'", "INVALID_RISK_DECISION");
        }

        try
        {
            credit.AssignRiskScore(request.RiskScore, decision);
            await creditRepository.UpdateAsync(credit, cancellationToken);

            if (credit.Status == CreditStatus.Active)
            {
                var client = await clientRepository.GetByIdAsync(credit.ClientId, cancellationToken);
                if (client is not null)
                {
                    client.ApplyDebtPenalty(credit.MonthlyPayment());
                    await clientRepository.UpdateAsync(client, cancellationToken);
                    logger.LogInformation(
                        "Debt penalty applied to client {ClientId}. New score={Score}",
                        client.Id, client.CreditScore);
                }
            }

            logger.LogInformation(
                "Risk assessment processed for credit {CreditId}: Score={RiskScore}, Decision={Decision}",
                credit.Id, request.RiskScore, request.Decision);

            return Result<CreditDto>.Success(credit.ToDto());
        }
        catch (InvalidCreditOperationException ex)
        {
            logger.LogWarning(ex, "Invalid risk assessment operation for credit {CreditId}", request.CreditId);
            return Result<CreditDto>.Failure(ex.Message, "INVALID_CREDIT_OPERATION");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing risk assessment for credit {CreditId}", request.CreditId);
            return Result<CreditDto>.Failure("An unexpected error occurred", "INTERNAL_ERROR");
        }
    }
}
