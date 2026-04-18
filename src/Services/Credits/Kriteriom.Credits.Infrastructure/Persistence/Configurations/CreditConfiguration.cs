using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kriteriom.Credits.Infrastructure.Persistence.Configurations;

public class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        builder.ToTable("credits");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.InterestRate)
            .HasColumnName("interest_rate")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(c => c.TermMonths)
            .HasColumnName("term_months")
            .HasDefaultValue(36)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.RiskScore)
            .HasColumnName("risk_score")
            .HasPrecision(5, 2);

        builder.Property(c => c.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(1000);

        builder.Property(c => c.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        builder.HasIndex(c => c.ClientId)
            .HasDatabaseName("ix_credits_client_id");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_credits_status");

        builder.HasIndex(c => new { c.ClientId, c.Status })
            .HasDatabaseName("ix_credits_client_id_status");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_credits_created_at");

        builder.Ignore("_domainEvents");
    }
}
