using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Kriteriom.BatchProcessor.Dashboard;
using Kriteriom.BatchProcessor.Jobs;
using Kriteriom.BatchProcessor.Persistence;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting BatchProcessor Service");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/batch");

    builder.Host.AddServiceSerilog(enrichWithMachineInfo: true);

    builder.Services.AddServiceDatabase<BatchDbContext>(builder.Configuration, npgsqlOptions: npgsql =>
        npgsql.MigrationsHistoryTable("__batch_ef_migrations_history"));

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("DefaultConnection"))));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 4;
        options.Queues = ["batch", "seed", "default"];
    });

    var creditsBaseUrl = builder.Configuration["CreditsApiBaseUrl"] ?? "http://credits-api:5001";

    builder.Services.AddHttpClient("credits-api", client =>
    {
        client.BaseAddress = new Uri(creditsBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddResilienceHandler("credits-api-resilience", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            OnRetry = args =>
            {
                Log.Warning("Credits API retry {Attempt} after {Delay}s",
                    args.AttemptNumber, args.RetryDelay.TotalSeconds);
                return default;
            }
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(30),
            OnOpened = args =>
            {
                Log.Warning("Credits API circuit breaker OPEN for {Duration}s", args.BreakDuration.TotalSeconds);
                return default;
            },
            OnClosed = _ =>
            {
                Log.Information("Credits API circuit breaker CLOSED");
                return default;
            }
        });
    });

    builder.Services.AddServiceRabbitMq(builder.Configuration, useStandardRetry: false);

    builder.Services.AddServiceTelemetry(builder.Configuration, "batch-processor", includeEfCore: true);

    builder.Services.AddTransient<CreditStatusRecalculationJob>();
    builder.Services.AddScoped<Kriteriom.BatchProcessor.Services.IBatchStatusService,
        Kriteriom.BatchProcessor.Services.BatchStatusService>();
    builder.Services.AddTransient<SeedTestDataJob>();

    builder.Services.AddControllers();
    builder.Services.AddServiceSwagger("Kriteriom Batch Processor",
        "Batch processing service with Hangfire for credit status recalculation");

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<BatchDbContext>("batch-database");

    var app = builder.Build();

    await app.RunMigrationsAsync<BatchDbContext>();

    app.UseSerilogRequestLogging(opts =>
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");

    app.UseServiceSwagger("Batch Processor");
    app.UseServiceMetrics("batch-processor");

    app.UseRouting();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAuthFilter(app.Configuration)]
    });

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "BatchProcessor terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
