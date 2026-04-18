using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetCredit;

public record GetCreditQuery(Guid CreditId) : IQuery<Result<CreditDto>>, ICacheable
{
    public string CacheKey => $"credit:{CreditId}";
    public TimeSpan CacheDuration => TimeSpan.FromSeconds(30);
}
