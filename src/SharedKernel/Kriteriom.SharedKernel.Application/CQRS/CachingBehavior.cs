using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Kriteriom.SharedKernel.CQRS;

/// <summary>
/// Pipeline behavior that caches responses for requests implementing ICacheable.
/// Only queries should implement ICacheable — commands must never be cached.
/// </summary>
public class CachingBehavior<TRequest, TResponse>(
    IDistributedCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheable cacheable)
            return await next();

        var cached = await cache.GetAsync(cacheable.CacheKey, cancellationToken);
        if (cached is not null)
        {
            logger.LogDebug("[Cache] HIT for {Key}", cacheable.CacheKey);
            return JsonSerializer.Deserialize<TResponse>(Encoding.UTF8.GetString(cached))!;
        }

        logger.LogDebug("[Cache] MISS for {Key}", cacheable.CacheKey);
        var response = await next();

        var serialized = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheable.CacheDuration
        };
        await cache.SetAsync(cacheable.CacheKey, serialized, options, cancellationToken);

        return response;
    }
}
