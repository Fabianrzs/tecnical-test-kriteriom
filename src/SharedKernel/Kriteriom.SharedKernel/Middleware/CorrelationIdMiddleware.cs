using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Kriteriom.SharedKernel.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[Header].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[Header] = correlationId;
        context.Items["CorrelationId"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogDebug("Request {Method} {Path} — CorrelationId {CorrelationId}",
                context.Request.Method, context.Request.Path, correlationId);

            await next(context);
        }
    }
}
