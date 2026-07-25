using ExcelETL.Domain.Archiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class GeneratedFileRecordConfiguration : IEntityTypeConfiguration<GeneratedFileRecord>
{
    public void Configure(EntityTypeBuilder<GeneratedFileRecord> builder)
    {
        builder.ToTable("GeneratedFileRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.EquipementRepere).HasMaxLength(200);
        builder.Property(r => r.SourceFileName).IsRequired().HasMaxLength(260);
        builder.Property(r => r.SourceFilePath).IsRequired().HasMaxLength(1024);
        builder.Property(r => r.TargetFileName).HasMaxLength(260);
        builder.Property(r => r.TargetFilePath).HasMaxLength(1024);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);

        // ImportProfileId/ExportProfileId are plain denormalized Guid values (no EF relationship,
        // no FK constraint, no navigation -- GeneratedFileRecord never navigates to ImportProfile/
        // ExportProfile) -- explicitly configuring them as scalar properties is required, not
        // cosmetic: EF Core's foreign-key-by-convention discovery otherwise matches the
        // "<PrincipalEntityTypeName>Id" naming pattern against the ImportProfile/ExportProfile
        // entity types already registered on this same DbContext and silently turns them into a
        // shadow relationship, which then breaks GeneratedFileRecord's constructor-binding
        // materialization ("Cannot bind 'importProfileId', 'exportProfileId'").
        builder.Property(r => r.ImportProfileId).IsRequired();
        builder.Property(r => r.ExportProfileId);

        // EquipementRepere: the sole search criterion (SearchAsync), see EfGeneratedFileArchiveStore.
        // GeneratedAtUtc: SearchAsync's own sort key, always descending.
        builder.HasIndex(r => r.EquipementRepere);
        builder.HasIndex(r => r.GeneratedAtUtc);
    }
}
