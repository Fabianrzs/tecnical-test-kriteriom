using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kriteriom.SharedKernel.Extensions;

public static class RedisExtensions
{
    public static IServiceCollection AddServiceRedisCache(
        this IServiceCollection services,
        IConfiguration configuration,
        string instanceName)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "redis:6379";
            options.InstanceName = instanceName;
        });

        return services;
    }
}
