using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.Credits.Infrastructure.Messaging;
using Kriteriom.Credits.Infrastructure.Persistence;
using Kriteriom.Credits.Infrastructure.Persistence.Repositories;
using Kriteriom.Credits.Infrastructure.Seeding;
using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.Contracts.Idempotency;
using Kriteriom.SharedKernel.Infrastructure.Idempotency;
using Kriteriom.SharedKernel.Infrastructure.Messaging;
using Kriteriom.SharedKernel.Infrastructure.Persistence;
using Kriteriom.SharedKernel.Messaging;
using Kriteriom.SharedKernel.Outbox;
using Microsoft.Extensions.Logging;
using Polly.Registry;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace Kriteriom.Credits.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddCreditsInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddDbContext<CreditsDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(CreditsDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });

            options.AddInterceptors(sp.GetRequiredService<DomainEventsToOutboxInterceptor>());
        });

        services.AddScoped<IDomainEventMapper, CreditsDomainEventMapper>();
        services.AddScoped<DomainEventsToOutboxInterceptor>();

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<CreditsDbContext>());

        services.AddScoped<ICreditRepository, CreditRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = config.GetConnectionString("Redis");
            options.InstanceName = "credits_";
        });

        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitConfig = config.GetSection("RabbitMQ");
                cfg.Host(rabbitConfig["Host"] ?? "localhost", h =>
                {
                    h.Username(rabbitConfig["Username"] ?? "guest");
                    h.Password(rabbitConfig["Password"] ?? "guest");
                });

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15)));

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddResiliencePipeline("outbox-retry", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });
        });

        
        IReadOnlyDictionary<string, Type> eventTypeRegistry = new Dictionary<string, Type>
        {
            [nameof(CreditCreatedIntegrationEvent)] = typeof(CreditCreatedIntegrationEvent),
            [nameof(CreditUpdatedIntegrationEvent)] = typeof(CreditUpdatedIntegrationEvent),
            [nameof(RiskAssessedIntegrationEvent)]  = typeof(RiskAssessedIntegrationEvent),
            [nameof(ClientCreatedIntegrationEvent)] = typeof(ClientCreatedIntegrationEvent),
            [nameof(ClientUpdatedIntegrationEvent)] = typeof(ClientUpdatedIntegrationEvent),
        };

        services.AddHostedService(sp => new OutboxProcessorService(
            eventTypeRegistry,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
            sp.GetRequiredService<ILogger<OutboxProcessorService>>()));

        if (config.GetValue<bool>("FeatureFlags:SeedTestData"))
            services.AddHostedService<CreditsDataSeeder>();

        return services;
    }
}
