using ExcelETL.Domain.Archiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class ArchiveRetentionSettingConfiguration : IEntityTypeConfiguration<ArchiveRetentionSetting>
{
    public void Configure(EntityTypeBuilder<ArchiveRetentionSetting> builder)
    {
        builder.ToTable("ArchiveRetentionSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.RetentionDays).IsRequired();
    }
}
