using Kriteriom.Audit.API.Consumers;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.Audit.Infrastructure.Persistence;
using Kriteriom.Audit.Infrastructure.Repositories;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting Audit API");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/audit");

    builder.Host.AddServiceSerilog();

    builder.Services.AddServiceDatabase<AuditDbContext>(builder.Configuration, npgsqlOptions: npgsql =>
        npgsql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName));

    builder.Services.AddScoped<IAuditRepository, AuditRepository>();

    builder.Services.AddServiceRabbitMq(builder.Configuration, x =>
    {
        x.AddConsumer<CreditCreatedAuditConsumer>();
        x.AddConsumer<CreditUpdatedAuditConsumer>();
        x.AddConsumer<RiskAssessedAuditConsumer>();
        x.AddConsumer<NotificationPermanentlyFailedAuditConsumer>();
    });

    builder.Services.AddServiceTelemetry(builder.Configuration, "audit-api");

    builder.Services.AddControllers();
    builder.Services.AddServiceSwagger("Kriteriom Audit API",
        "Immutable audit log service for all integration events");

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "database");

    var app = builder.Build();

    await app.RunMigrationsAsync<AuditDbContext>();

    app.UseCommonMiddleware();
    app.UseSerilogRequestLogging();
    app.UseServiceSwagger("Audit API");
    app.UseServiceMetrics("audit-api");

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
