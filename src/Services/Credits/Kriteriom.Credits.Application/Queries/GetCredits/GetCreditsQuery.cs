using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetCredits;

public record GetCreditsQuery(
    int Page = 1,
    int PageSize = 20,
    CreditStatus? Status = null,
    Guid? ClientId = null) : IQuery<Result<PagedResult<CreditDto>>>, ICacheable
{
    public string CacheKey => $"credits:page:{Page}:size:{PageSize}:status:{Status}:client:{ClientId}";
    public TimeSpan CacheDuration => TimeSpan.FromSeconds(15);
}
