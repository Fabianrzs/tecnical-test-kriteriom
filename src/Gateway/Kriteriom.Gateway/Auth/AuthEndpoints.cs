namespace Kriteriom.Gateway.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", (
            LoginRequest req,
            TokenService svc,
            RefreshTokenStore store,
            IConfiguration config,
            ILogger<TokenService> logger) =>
        {
            var result = svc.GenerateToken(req.Username, req.Password);
            if (result is null)
            {
                logger.LogWarning("Failed login attempt for user {Username}", req.Username);
                return Results.Unauthorized();
            }

            var refreshToken = store.Generate(result.Username, result.Role);
            logger.LogInformation("Token issued for user {Username}", req.Username);

            return Results.Ok(new
            {
                accessToken  = result.AccessToken,
                refreshToken,
                tokenType    = "Bearer",
                expiresIn    = config.GetValue<int>("Jwt:ExpiryMinutes", 60) * 60
            });
        })
        .WithName("GetToken")
        .WithSummary("Issue JWT access + refresh tokens")
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        app.MapPost("/auth/refresh", (
            RefreshRequest req,
            TokenService svc,
            RefreshTokenStore store,
            IConfiguration config,
            ILogger<TokenService> logger) =>
        {
            var identity = store.Validate(req.RefreshToken);
            if (identity is null)
            {
                logger.LogWarning("Invalid or expired refresh token");
                return Results.Unauthorized();
            }

            store.Revoke(req.RefreshToken);

            var newAccess  = svc.GenerateAccessToken(identity.Value.Username, identity.Value.Role);
            var newRefresh = store.Generate(identity.Value.Username, identity.Value.Role);

            logger.LogInformation("Token refreshed for user {Username}", identity.Value.Username);

            return Results.Ok(new
            {
                accessToken  = newAccess,
                refreshToken = newRefresh,
                tokenType    = "Bearer",
                expiresIn    = config.GetValue<int>("Jwt:ExpiryMinutes", 60) * 60
            });
        })
        .WithName("RefreshToken")
        .WithSummary("Rotate refresh token and issue a new access token")
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        app.MapPost("/auth/logout", (
            RefreshRequest req,
            RefreshTokenStore store,
            ILogger<TokenService> logger) =>
        {
            store.Revoke(req.RefreshToken);
            logger.LogInformation("Refresh token revoked");
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Revoke a refresh token")
        .RequireAuthorization();

        return app;
    }
}

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
