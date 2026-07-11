using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class ExtractionHistoryConfiguration : IEntityTypeConfiguration<ExtractionHistory>
{
    public void Configure(EntityTypeBuilder<ExtractionHistory> builder)
    {
        builder.ToTable("ExtractionHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.JobTimestamp)
            .IsRequired();

        builder.Property(h => h.SourceFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(h => h.StoredFilePath)
            .HasMaxLength(1024);

        builder.Property(h => h.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.CompletedAtUtc);

        builder.Ignore(h => h.Duration);
    }
}
