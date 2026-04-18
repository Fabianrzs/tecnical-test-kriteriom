using System.Diagnostics;
using Yarp.ReverseProxy.Model;

namespace Kriteriom.Gateway.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationHeader = "X-Correlation-Id";
        if (!context.Request.Headers.TryGetValue(correlationHeader, out var value))
        {
            context.Request.Headers[correlationHeader] = Guid.NewGuid().ToString();
        }

        var correlationId = value.FirstOrDefault() ?? string.Empty;
        context.Response.Headers[correlationHeader] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var upstream = "unknown";

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            if (proxyFeature?.Route?.Config?.ClusterId is { } clusterId)
                upstream = clusterId;

            logger.LogInformation(
                "Gateway routed {Method} {Path} → {Upstream} [{StatusCode}] in {DurationMs}ms (CorrelationId={CorrelationId})",
                context.Request.Method,
                context.Request.Path,
                upstream,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
}
