using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetCredits;

public record GetCreditsQuery(
    int           Page       = 1,
    int           PageSize   = 20,
    CreditStatus? Status     = null,
    Guid?         ClientId   = null,
    decimal?      AmountMin  = null,
    decimal?      AmountMax  = null,
    DateTime?     DateFrom   = null,
    DateTime?     DateTo     = null,
    string?       RiskLevel  = null,
    string?       ClientName = null
) : IQuery<Result<PagedResult<CreditDto>>>, ICacheable
{
    public string CacheKey =>
        $"credits:p{Page}:s{PageSize}:st{Status}:cl{ClientId}:amin{AmountMin}:amax{AmountMax}" +
        $":df{DateFrom:yyyyMMdd}:dt{DateTo:yyyyMMdd}:rl{RiskLevel}:cn{ClientName}";

    public TimeSpan CacheDuration => TimeSpan.FromSeconds(15);
}
