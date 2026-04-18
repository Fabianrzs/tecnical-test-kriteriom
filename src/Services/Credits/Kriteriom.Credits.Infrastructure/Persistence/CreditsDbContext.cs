using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Infrastructure.Persistence.Configurations;
using Kriteriom.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.Credits.Infrastructure.Persistence;

public class CreditsDbContext(DbContextOptions<CreditsDbContext> options) : DbContext(options)
{
    public DbSet<Credit> Credits => Set<Credit>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CreditConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(500);
            entity.Property(e => e.Response).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
