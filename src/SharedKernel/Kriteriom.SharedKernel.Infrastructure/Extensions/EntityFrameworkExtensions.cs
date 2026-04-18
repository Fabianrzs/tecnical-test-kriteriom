using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Kriteriom.SharedKernel.Extensions;

public static class EntityFrameworkExtensions
{
    public static IServiceCollection AddServiceDatabase<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection",
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptions = null) where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' is required.");

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                npgsqlOptions?.Invoke(npgsql);
            }));

        return services;
    }

    public static async Task RunMigrationsAsync<TContext>(this WebApplication app) where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        try
        {
            logger.LogInformation("Applying database migrations for {Context}…", typeof(TContext).Name);
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully for {Context}", typeof(TContext).Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying database migrations for {Context}", typeof(TContext).Name);
            throw;
        }
    }
}
