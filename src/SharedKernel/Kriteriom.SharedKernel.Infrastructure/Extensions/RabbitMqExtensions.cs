using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kriteriom.SharedKernel.Extensions;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddServiceRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null,
        bool useStandardRetry = true)
    {
        services.AddMassTransit(x =>
        {
            registerConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbit = configuration.GetSection("RabbitMQ");
                cfg.Host(rabbit["Host"] ?? "localhost", h =>
                {
                    h.Username(rabbit["Username"] ?? "guest");
                    h.Password(rabbit["Password"] ?? "guest");
                });

                if (useStandardRetry)
                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15)));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
