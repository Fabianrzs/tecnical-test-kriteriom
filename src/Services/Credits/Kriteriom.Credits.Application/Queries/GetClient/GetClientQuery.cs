using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetClient;

public record GetClientQuery(Guid ClientId) : IQuery<Result<ClientDto>>, ICacheable
{
    public string CacheKey     => $"client:{ClientId}";
    public TimeSpan CacheDuration => TimeSpan.FromSeconds(60);
}
