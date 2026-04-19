using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Queries.GetCredits;

public class GetCreditsQueryHandler(
    ICreditRepository creditRepository,
    ILogger<GetCreditsQueryHandler> logger)
    : IRequestHandler<GetCreditsQuery, Result<PagedResult<CreditDto>>>
{
    public async Task<Result<PagedResult<CreditDto>>> Handle(GetCreditsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await creditRepository.GetAllAsync(
            request.Page,
            request.PageSize,
            request.Status,
            request.ClientId,
            request.AmountMin,
            request.AmountMax,
            request.DateFrom,
            request.DateTo,
            request.RiskLevel,
            request.ClientName,
            cancellationToken);

        var dtos = items.Select(c => c.ToDto()).ToList();

        var pagedResult = new PagedResult<CreditDto>
        {
            Items      = dtos,
            TotalCount = total,
            Page       = request.Page,
            PageSize   = request.PageSize
        };

        logger.LogDebug(
            "Retrieved {Count} credits (page {Page}/{TotalPages})",
            dtos.Count, request.Page, pagedResult.TotalPages);

        return Result<PagedResult<CreditDto>>.Success(pagedResult);
    }
}
