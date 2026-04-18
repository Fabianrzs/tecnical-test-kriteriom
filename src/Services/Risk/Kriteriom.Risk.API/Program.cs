using Kriteriom.Risk.API.Consumers;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting Risk API");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/risk");

    builder.Host.AddServiceSerilog();

    builder.Services.AddServiceRabbitMq(builder.Configuration, x =>
    {
        x.AddConsumer<CreditCreatedConsumer>();
        x.AddConsumer<CreditRiskEvaluationRequestedConsumer>();
    });

    var creditsBaseUrl = builder.Configuration["CreditsApiBaseUrl"] ?? "http://credits-api:5001";

    builder.Services.AddHttpClient("credits-api", client =>
    {
        client.BaseAddress = new Uri(creditsBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddServiceTelemetry(builder.Configuration, "risk-api");
    builder.Services.AddServiceRedisCache(builder.Configuration, "risk:");

    builder.Services.AddHealthChecks()
        .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379", name: "redis");

    var app = builder.Build();

    app.UseCommonMiddleware();
    app.UseSerilogRequestLogging();
    app.UseServiceMetrics("risk-api");

    app.UseRouting();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new { service = "risk-api", status = "running" }));

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Risk API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
