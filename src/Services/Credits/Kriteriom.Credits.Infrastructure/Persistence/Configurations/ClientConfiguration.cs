using Kriteriom.Credits.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kriteriom.Credits.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(c => c.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.MonthlyIncome)
            .HasColumnName("monthly_income")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.CreditScore)
            .HasColumnName("credit_score")
            .IsRequired();

        builder.Property(c => c.EmploymentStatus)
            .HasColumnName("employment_status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property<int>("Version")
            .HasColumnName("version")
            .IsConcurrencyToken();

        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasDatabaseName("ix_clients_email");

        builder.HasIndex(c => c.DocumentNumber)
            .IsUnique()
            .HasDatabaseName("ix_clients_document_number");

        builder.Ignore("_domainEvents");
    }
}
