using Kriteriom.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.Notifications.Infrastructure.Persistence;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationRecord>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.CreditId).HasColumnName("credit_id").IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
            e.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(256).IsRequired();
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(512).IsRequired();
            e.Property(x => x.Body).HasColumnName("body").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.SentAt).HasColumnName("sent_at");

            e.HasIndex(x => x.EventId).HasDatabaseName("ix_notifications_event_id").IsUnique()
                .HasFilter("event_id IS NOT NULL");
            e.HasIndex(x => x.CreditId).HasDatabaseName("ix_notifications_credit_id");
            e.HasIndex(x => x.Status).HasDatabaseName("ix_notifications_status");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_notifications_created_at");
        });
    }
}
