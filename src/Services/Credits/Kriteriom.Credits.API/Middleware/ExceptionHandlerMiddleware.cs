using System.Net;
using System.Text.Json;
using FluentValidation;
using Kriteriom.Credits.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Kriteriom.Credits.API.Middleware;

public class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var (statusCode, problemDetails) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Title = "Validation Error",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = "One or more validation errors occurred",
                    Extensions =
                    {
                        ["errors"] = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }),
                        ["correlationId"] = correlationId
                    }
                }),

            CreditNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Title = "Resource Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = notFoundEx.Message,
                    Extensions = { ["correlationId"] = correlationId }
                }),

            InvalidCreditOperationException domainEx => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Title = "Invalid Operation",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = domainEx.Message,
                    Extensions = { ["correlationId"] = correlationId }
                }),

            _ => (
                HttpStatusCode.InternalServerError,
                new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = "An unexpected error occurred. Please try again later.",
                    Extensions = { ["correlationId"] = correlationId }
                })
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled exception for request {Method} {Path} CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path, correlationId);
        }
        else
        {
            logger.LogWarning(exception,
                "Handled exception {ExceptionType} for request {Method} {Path} CorrelationId={CorrelationId}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, correlationId);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}
