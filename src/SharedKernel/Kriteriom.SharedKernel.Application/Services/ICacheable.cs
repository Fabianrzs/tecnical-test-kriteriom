namespace Kriteriom.SharedKernel.CQRS;

/// <summary>
/// Marker interface for queries whose results should be stored in IDistributedCache.
/// </summary>
public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}
