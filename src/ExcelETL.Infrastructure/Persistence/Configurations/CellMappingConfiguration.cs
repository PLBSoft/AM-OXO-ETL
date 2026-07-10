using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class CellMappingConfiguration : IEntityTypeConfiguration<CellMapping>
{
    public void Configure(EntityTypeBuilder<CellMapping> builder)
    {
        builder.ToTable("CellMappings");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SourceCell)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(m => m.TargetPropertyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.DataType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
