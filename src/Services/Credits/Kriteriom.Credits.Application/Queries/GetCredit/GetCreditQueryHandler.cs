using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Queries.GetCredit;

public class GetCreditQueryHandler(
    ICreditRepository creditRepository,
    ILogger<GetCreditQueryHandler> logger)
    : IRequestHandler<GetCreditQuery, Result<CreditDto>>
{
    public async Task<Result<CreditDto>> Handle(GetCreditQuery request, CancellationToken cancellationToken)
    {
        var credit = await creditRepository.GetByIdAsync(request.CreditId, cancellationToken);

        if (credit is not null) return Result<CreditDto>.Success(credit.ToDto());
        logger.LogWarning("Credit {CreditId} not found", request.CreditId);
        return Result<CreditDto>.Failure($"Credit {request.CreditId} not found", "CREDIT_NOT_FOUND");

    }
}
