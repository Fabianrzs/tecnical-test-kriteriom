using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Kriteriom.BatchProcessor.Dashboard;
using Kriteriom.BatchProcessor.Jobs;
using Kriteriom.BatchProcessor.Persistence;
using Kriteriom.SharedKernel.Vault;
using MassTransit;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Prometheus;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BatchProcessor Service");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/batch");

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is required.");

    builder.Services.AddDbContext<BatchDbContext>(options =>
        options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__batch_ef_migrations_history")));

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 4;
        options.Queues = new[] { "batch", "seed", "default" };
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
            OnClosed = args =>
            {
                Log.Information("Credits API circuit breaker CLOSED");
                return default;
            }
        });
    });

    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "admin");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "admin123");
            });

            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("batch-processor", serviceVersion: "1.0.0"))
                .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = true)
                .AddJaegerExporter(opts =>
                {
                    opts.AgentHost = builder.Configuration["Jaeger:Host"] ?? "localhost";
                    opts.AgentPort = int.Parse(builder.Configuration["Jaeger:Port"] ?? "6831");
                });
        });

    builder.Services.AddTransient<CreditStatusRecalculationJob>();
    builder.Services.AddScoped<Kriteriom.BatchProcessor.Services.IBatchStatusService,
        Kriteriom.BatchProcessor.Services.BatchStatusService>();
    builder.Services.AddTransient<SeedTestDataJob>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Kriteriom Batch Processor",
            Version = "v1",
            Description = "Batch processing service with Hangfire for credit status recalculation"
        });
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<BatchDbContext>("batch-database");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<BatchDbContext>();
        var startLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            startLogger.LogInformation("Applying batch database migrations…");
            await dbContext.Database.MigrateAsync();
            startLogger.LogInformation("Batch database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            startLogger.LogError(ex, "Error applying batch database migrations");
            throw;
        }
    }

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });
    
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Batch Processor v1");
        c.RoutePrefix = "swagger";
    });

    app.UseMetricServer();
    app.UseHttpMetrics(options =>
    {
        options.AddCustomLabel("service", _ => "batch-processor");
    });

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

