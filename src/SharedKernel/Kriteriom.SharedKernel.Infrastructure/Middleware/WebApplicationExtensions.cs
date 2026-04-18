using Microsoft.AspNetCore.Builder;
using Prometheus;

namespace Kriteriom.SharedKernel.Middleware;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder UseCommonMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseServiceMetrics(this IApplicationBuilder app, string serviceName)
    {
        app.UseMetricServer();
        app.UseHttpMetrics(options =>
        {
            options.AddCustomLabel("service", _ => serviceName);
        });
        return app;
    }
}
