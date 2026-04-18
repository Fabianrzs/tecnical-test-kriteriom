using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kriteriom.SharedKernel.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        logger.LogError(exception,
            "Unhandled exception {ExceptionType} — {Method} {Path} CorrelationId={CorrelationId}",
            exception.GetType().Name, context.Request.Method, context.Request.Path, correlationId);

        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = (int)HttpStatusCode.InternalServerError,
            Detail = "An unexpected error occurred. Please try again later.",
            Extensions = { ["correlationId"] = correlationId }
        };

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
