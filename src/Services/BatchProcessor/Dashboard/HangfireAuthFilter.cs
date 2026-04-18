using System.Text;
using Hangfire.Dashboard;

namespace Kriteriom.BatchProcessor.Dashboard;

public class HangfireAuthFilter(IConfiguration config) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(httpContext);
            return false;
        }

        var encoded = authHeader["Basic ".Length..].Trim();
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            Challenge(httpContext);
            return false;
        }

        var sep = decoded.IndexOf(':');
        if (sep < 0) { Challenge(httpContext); return false; }

        var user     = decoded[..sep];
        var password = decoded[(sep + 1)..];

        var expectedUser     = config["Hangfire:DashboardUser"]     ?? "admin";
        var expectedPassword = config["Hangfire:DashboardPassword"] ?? "Hangf1r3@2026";

        if (user != expectedUser || password != expectedPassword)
        {
            Challenge(httpContext);
            return false;
        }

        return true;
    }

    private static void Challenge(HttpContext ctx)
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}
