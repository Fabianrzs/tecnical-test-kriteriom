using System.Text;
using Kriteriom.Credits.Application.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Idempotency;

public class IdempotencyService(IDistributedCache cache, ILogger<IdempotencyService> logger)
    : IIdempotencyService
{
    private const string KeyPrefix = "idempotency:credits:";
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var fullKey = $"{KeyPrefix}{key}";

        try
        {
            var bytes = await cache.GetAsync(fullKey, ct);
            if (bytes is null)
                return null;

            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve idempotency key {Key} from Redis", fullKey);
            return null;
        }
    }

    public async Task SetAsync(string key, string response, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var fullKey = $"{KeyPrefix}{key}";
        var actualExpiry = expiry ?? DefaultExpiry;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(response);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = actualExpiry
            };

            await cache.SetAsync(fullKey, bytes, options, ct);

            logger.LogDebug(
                "Stored idempotency response for key {Key} with expiry {Expiry}",
                fullKey, actualExpiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store idempotency key {Key} in Redis", fullKey);
        }
    }
}
