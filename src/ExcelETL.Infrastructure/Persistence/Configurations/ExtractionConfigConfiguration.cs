using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class ExtractionConfigConfiguration : IEntityTypeConfiguration<ExtractionConfig>
{
    public void Configure(EntityTypeBuilder<ExtractionConfig> builder)
    {
        builder.ToTable("ExtractionConfigs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasMany(c => c.Sheets)
            .WithOne()
            .HasForeignKey("ExtractionConfigId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Sheets)
            .HasField("_sheets")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
