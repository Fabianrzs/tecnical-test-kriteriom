using Kriteriom.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.Audit.Infrastructure.Persistence;

public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("audit_records");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(e => e.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.EventId)
                .HasColumnName("event_id")
                .IsRequired();

            entity.Property(e => e.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(256);

            entity.Property(e => e.EntityId)
                .HasColumnName("entity_id");

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .IsRequired();

            entity.Property(e => e.OccurredOn)
                .HasColumnName("occurred_on")
                .IsRequired();

            entity.Property(e => e.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            entity.Property(e => e.ServiceName)
                .HasColumnName("service_name")
                .HasMaxLength(128);

            entity.HasIndex(e => e.EventId)
                .IsUnique()
                .HasDatabaseName("ix_audit_records_event_id");

            entity.HasIndex(e => e.EventType)
                .HasDatabaseName("ix_audit_records_event_type");

            entity.HasIndex(e => e.OccurredOn)
                .HasDatabaseName("ix_audit_records_occurred_on");

            entity.HasIndex(e => e.EntityId)
                .HasDatabaseName("ix_audit_records_entity_id");
        });
    }
}
