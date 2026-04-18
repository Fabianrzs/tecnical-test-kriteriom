using Microsoft.AspNetCore.Builder;

namespace Kriteriom.SharedKernel.Middleware;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Registers CorrelationId propagation and global exception handling for all services.
    /// Must be called before UseRouting.
    /// </summary>
    public static IApplicationBuilder UseCommonMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }
}
