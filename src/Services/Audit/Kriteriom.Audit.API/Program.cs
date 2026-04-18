using Kriteriom.Audit.API.Consumers;
using Kriteriom.Audit.Infrastructure.Persistence;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.Audit.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
    Log.Information("Starting Audit API");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/audit");

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    });

    builder.Services.AddDbContext<AuditDbContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
    });

    builder.Services.AddScoped<IAuditRepository, AuditRepository>();

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<CreditCreatedAuditConsumer>();
        x.AddConsumer<CreditUpdatedAuditConsumer>();
        x.AddConsumer<RiskAssessedAuditConsumer>();
        x.AddConsumer<NotificationPermanentlyFailedAuditConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
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

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("audit-api", serviceVersion: "1.0.0"))
                .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddJaegerExporter(opts =>
                {
                    opts.AgentHost = builder.Configuration["Jaeger:Host"] ?? "localhost";
                    opts.AgentPort = int.Parse(builder.Configuration["Jaeger:Port"] ?? "6831");
                });
        });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Kriteriom Audit API",
            Version = "v1",
            Description = "Immutable audit log service for all integration events"
        });
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "database");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Applying audit database migrations...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Audit database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying audit database migrations");
            throw;
        }
    }

    app.UseCommonMiddleware();
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Audit API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseMetricServer();
    app.UseHttpMetrics(options =>
    {
        options.AddCustomLabel("service", _ => "audit-api");
    });

    app.UseRouting();
    app.MapControllers();

    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Audit API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
