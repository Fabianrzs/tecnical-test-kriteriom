using Microsoft.EntityFrameworkCore;

namespace Kriteriom.BatchProcessor.Persistence;

public class BatchDbContext : DbContext
{
    public BatchDbContext(DbContextOptions<BatchDbContext> options) : base(options) { }

    public DbSet<BatchJobCheckpoint> BatchJobCheckpoints => Set<BatchJobCheckpoint>();
    public DbSet<BatchJobLog> BatchJobLogs => Set<BatchJobLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BatchJobCheckpoint>(entity =>
        {
            entity.ToTable("batch_job_checkpoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobName).HasColumnName("job_name").IsRequired().HasMaxLength(200);
            entity.Property(e => e.LastProcessedOffset).HasColumnName("last_processed_offset");
            entity.Property(e => e.TotalRecords).HasColumnName("total_records");
            entity.Property(e => e.ProcessedRecords).HasColumnName("processed_records");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(50);
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            entity.HasIndex(e => e.JobName).HasDatabaseName("ix_batch_job_checkpoints_job_name");
        });

        modelBuilder.Entity<BatchJobLog>(entity =>
        {
            entity.ToTable("batch_job_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobName).HasColumnName("job_name").IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).HasColumnName("message").IsRequired();
            entity.Property(e => e.Level).HasColumnName("level").IsRequired().HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasIndex(e => e.JobName).HasDatabaseName("ix_batch_job_logs_job_name");
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("ix_batch_job_logs_timestamp");
        });
    }
}
