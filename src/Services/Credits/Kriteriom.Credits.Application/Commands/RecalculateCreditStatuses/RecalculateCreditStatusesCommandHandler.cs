using Kriteriom.Credits.Application.Services;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.RecalculateCreditStatuses;

public class RecalculateCreditStatusesCommandHandler(
    ICreditRepository creditRepository,
    IUnitOfWork unitOfWork,
    ILogger<RecalculateCreditStatusesCommandHandler> logger)
    : IRequestHandler<RecalculateCreditStatusesCommand, Result<RecalculateCreditStatusesSummary>>
{
    public async Task<Result<RecalculateCreditStatusesSummary>> Handle(
        RecalculateCreditStatusesCommand request, CancellationToken cancellationToken)
    {
        var total     = await creditRepository.GetTotalCountAsync(cancellationToken);
        int processed = 0, updated = 0, errors = 0;

        logger.LogInformation("RecalculateCreditStatuses started. Total={Total}", total);

        for (int skip = 0; skip < total; skip += request.BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await creditRepository.GetForBatchProcessingAsync(
                request.BatchSize, skip, cancellationToken);

            foreach (var credit in batch)
            {
                processed++;
                var (newStatus, reason) = DetermineNewStatus(credit);
                if (newStatus is null) continue;

                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        credit.UpdateStatus(newStatus.Value, reason);
                        await creditRepository.UpdateAsync(credit, cancellationToken);
                    }, cancellationToken);

                    updated++;

                    logger.LogInformation(
                        "Credit {Id}: {Old} → {New} — {Reason}",
                        credit.Id, credit.Status, newStatus, reason);
                }
                catch (InvalidCreditOperationException ex)
                {
                    errors++;
                    logger.LogWarning("Credit {Id} skipped: {Error}", credit.Id, ex.Message);
                }
                catch (Exception ex)
                {
                    errors++;
                    logger.LogError(ex, "Credit {Id} failed unexpectedly", credit.Id);
                }
            }
        }

        logger.LogInformation(
            "RecalculateCreditStatuses done. Processed={Processed}, Updated={Updated}, Errors={Errors}",
            processed, updated, errors);

        return Result<RecalculateCreditStatusesSummary>.Success(
            new RecalculateCreditStatusesSummary(processed, updated, errors));
    }

    private static (CreditStatus? newStatus, string? reason) DetermineNewStatus(Credit credit)
    {
        if (credit.Status == CreditStatus.Active && credit.RiskScore > 30)
            return (CreditStatus.UnderReview,
                $"Risk score {credit.RiskScore:F1} supera el umbral permitido para créditos activos");

        if (credit.Status == CreditStatus.Pending && credit.CreatedAt < DateTime.UtcNow.AddDays(-7))
            return (CreditStatus.Rejected,
                "Solicitud expirada — sin evaluación de riesgo en 7 días");

        if (credit.Status == CreditStatus.UnderReview && credit.UpdatedAt < DateTime.UtcNow.AddDays(-30))
            return (CreditStatus.Rejected,
                "Revisión manual vencida — más de 30 días sin resolución");

        return (null, null);
    }
}
