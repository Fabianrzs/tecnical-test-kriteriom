using Kriteriom.Notifications.API.Consumers;
using Kriteriom.Notifications.Domain.Repositories;
using Kriteriom.Notifications.Domain.Services;
using Kriteriom.Notifications.Infrastructure.Persistence;
using Kriteriom.Notifications.Infrastructure.Repositories;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting Notifications API");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/infra");

    builder.Host.AddServiceSerilog();

    builder.Services.AddServiceDatabase<NotificationsDbContext>(builder.Configuration);

    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
    builder.Services.AddSingleton<INotificationSender,
        Kriteriom.Notifications.Infrastructure.Services.LogNotificationSender>();

    builder.Services.AddServiceRabbitMq(builder.Configuration, x =>
    {
        x.AddConsumer<CreditCreatedNotificationConsumer>();
        x.AddConsumer<RiskAssessedNotificationConsumer>();
        x.AddConsumer<NotificationDeliveryFailedConsumer>();
    });

    builder.Services.AddServiceTelemetry(builder.Configuration, "notifications-api");

    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    await app.RunMigrationsAsync<NotificationsDbContext>();

    app.UseCommonMiddleware();
    app.UseSerilogRequestLogging();
    app.UseServiceMetrics("notifications-api");

    app.UseRouting();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new { service = "notifications-api", status = "running" }));

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Notifications API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
