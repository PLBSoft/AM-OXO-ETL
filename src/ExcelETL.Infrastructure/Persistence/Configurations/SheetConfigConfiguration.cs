using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class SheetConfigConfiguration : IEntityTypeConfiguration<SheetConfig>
{
    public void Configure(EntityTypeBuilder<SheetConfig> builder)
    {
        builder.ToTable("SheetConfigs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SheetName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.SheetIndex)
            .IsRequired();

        builder.HasMany(s => s.CellMappings)
            .WithOne()
            .HasForeignKey("SheetConfigId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.CellMappings)
            .HasField("_cellMappings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
