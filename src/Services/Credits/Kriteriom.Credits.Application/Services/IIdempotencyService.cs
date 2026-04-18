namespace Kriteriom.Credits.Application.Services;

public interface IIdempotencyService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string response, TimeSpan? expiry = null, CancellationToken ct = default);
}
