using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kriteriom.BatchProcessor.Persistence;

/// <summary>
/// Used by EF Core tools (migrations) at design time.
/// </summary>
public class BatchDbContextFactory : IDesignTimeDbContextFactory<BatchDbContext>
{
    public BatchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BatchDbContext>();

        // Design-time connection string — overridden at runtime via appsettings
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=credits_db;Username=admin;Password=admin123",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__batch_ef_migrations_history"));

        return new BatchDbContext(optionsBuilder.Options);
    }
}
