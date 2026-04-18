using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Queries.GetCreditStats;

public class GetCreditStatsQueryHandler(
    ICreditRepository creditRepository,
    IClientRepository clientRepository,
    ILogger<GetCreditStatsQueryHandler> logger)
    : IRequestHandler<GetCreditStatsQuery, Result<CreditStatsDto>>
{
    public async Task<Result<CreditStatsDto>> Handle(GetCreditStatsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var statusCounts = await creditRepository.GetStatusCountsAsync(cancellationToken);
            var totalClients = await clientRepository.GetTotalCountAsync(cancellationToken);

            var approved   = statusCounts.GetValueOrDefault((int)CreditStatus.Active, 0);
            var rejected   = statusCounts.GetValueOrDefault((int)CreditStatus.Rejected, 0);
            var pending    = statusCounts.GetValueOrDefault((int)CreditStatus.Pending, 0);
            var underReview = statusCounts.GetValueOrDefault((int)CreditStatus.UnderReview, 0);
            var closed     = statusCounts.GetValueOrDefault((int)CreditStatus.Closed, 0);
            var defaulted  = statusCounts.GetValueOrDefault((int)CreditStatus.Defaulted, 0);

            var totalCredits = statusCounts.Values.Sum();
            var decided      = approved + rejected;
            var approvalRate = decided > 0 ? Math.Round((double)approved / decided * 100, 1) : 0.0;

            return Result<CreditStatsDto>.Success(new CreditStatsDto(
                totalCredits, totalClients, approvalRate,
                pending, underReview, approved, rejected, closed, defaulted));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving credit statistics");
            return Result<CreditStatsDto>.Failure("Error retrieving statistics", "INTERNAL_ERROR");
        }
    }
}
