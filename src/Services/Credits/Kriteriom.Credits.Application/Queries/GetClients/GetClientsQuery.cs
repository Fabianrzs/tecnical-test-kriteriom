using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetClients;

public record GetClientsQuery(int Page = 1, int PageSize = 20) : IQuery<Result<PagedResult<ClientDto>>>, ICacheable
{
    public string CacheKey     => $"clients:page:{Page}:size:{PageSize}";
    public TimeSpan CacheDuration => TimeSpan.FromSeconds(30);
}
