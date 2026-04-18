using System.Text;
using System.Threading.RateLimiting;
using Kriteriom.Gateway.Auth;
using Kriteriom.Gateway.Middleware;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting API Gateway");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/auth");

    builder.Host.AddServiceSerilog(enrichWithMachineInfo: true);

    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddSingleton<RefreshTokenStore>();

    var jwtSecret   = builder.Configuration["Jwt:Secret"]   ?? "default-dev-secret-change-in-production";
    var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "kriteriom-api-gateway";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "kriteriom-services";

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer   = true,
                ValidIssuer      = jwtIssuer,
                ValidateAudience = true,
                ValidAudience    = jwtAudience,
                ClockSkew        = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("AdminOnly",     p => p.RequireRole("Admin"));
        opts.AddPolicy("AnalystPlus",   p => p.RequireRole("Admin", "Analyst"));
        opts.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    });

    builder.Services.AddRateLimiter(opts =>
    {
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opts.AddFixedWindowLimiter("api", limiter =>
        {
            limiter.Window           = TimeSpan.FromMinutes(1);
            limiter.PermitLimit      = 100;
            limiter.QueueLimit       = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
        opts.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.Window      = TimeSpan.FromMinutes(1);
            limiter.PermitLimit = 10;
            limiter.QueueLimit  = 0;
        });
    });

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.AddServiceTelemetry(builder.Configuration, "api-gateway");

    var creditsAddress = builder.Configuration["ReverseProxy:Clusters:credits-cluster:Destinations:credits-api:Address"]   ?? "http://credits-api:5001/";
    var auditAddress   = builder.Configuration["ReverseProxy:Clusters:audit-cluster:Destinations:audit-api:Address"]       ?? "http://audit-api:5003/";
    var batchAddress   = builder.Configuration["ReverseProxy:Clusters:batch-cluster:Destinations:batch-processor:Address"] ?? "http://batch-processor:5005/";

    builder.Services.AddHealthChecks()
        .AddUrlGroup(new Uri($"{creditsAddress.TrimEnd('/')}/health"), name: "credits-api",     tags: ["downstream"])
        .AddUrlGroup(new Uri($"{auditAddress.TrimEnd('/')}/health"),   name: "audit-api",       tags: ["downstream"])
        .AddUrlGroup(new Uri($"{batchAddress.TrimEnd('/')}/health"),   name: "batch-processor", tags: ["downstream"]);

    var app = builder.Build();

    app.UseCors();
    app.UseRateLimiter();

    app.UseSerilogRequestLogging(opts =>
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");

    app.UseServiceMetrics("api-gateway");
    app.UseMiddleware<RequestLoggingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapReverseProxy().RequireRateLimiting("api");

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/downstream", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("downstream")
    });

    app.MapGet("/", () => Results.Ok(new
    {
        service = "API Gateway",
        version = "1.0",
        auth    = "POST /auth/token — { username, password }",
        routes  = new[] { "/api/credits", "/api/audit", "/api/batch", "/hangfire" }
    }));

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "API Gateway terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
