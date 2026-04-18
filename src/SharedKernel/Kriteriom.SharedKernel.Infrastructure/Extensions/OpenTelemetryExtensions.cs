using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Kriteriom.SharedKernel.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddServiceTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeEfCore = false)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName, serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                    .AddHttpClientInstrumentation();

                if (includeEfCore)
                    tracing.AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = true);

                tracing.AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri($"http://{configuration["Jaeger:Host"] ?? "localhost"}:4317");
                });
            });

        return services;
    }
}
