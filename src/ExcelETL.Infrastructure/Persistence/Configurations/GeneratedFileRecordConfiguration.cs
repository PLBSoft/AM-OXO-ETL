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
        builder.Property(r => r.Username).HasMaxLength(GeneratedFileRecord.MaxUsernameLength);

        // Plain non-nullable int scalars -- EF Core maps them NOT NULL by convention with no extra
        // configuration needed for storage, but constructor-binding materialization (Id/GeneratedAtUtc/
        // etc. all use the same read-only-property + constructor-parameter shape on this type) requires
        // every bound property to be explicitly discovered/mapped first; an unreferenced property is
        // otherwise never added to the model, and EF then refuses to bind the matching constructor
        // parameter to it ("Cannot bind ... Note that only mapped properties can be bound").
        builder.Property(r => r.IsolementCount).IsRequired();
        builder.Property(r => r.PointCount).IsRequired();
        builder.Property(r => r.TacheMultipleCount).IsRequired();

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

        // Lot 072: a plain owned table, same convention as every other owned collection in this
        // project (ImportProfileSheetRules, etc.) -- not EF Core's .ToJson() mapping, which no
        // other type in this codebase uses; consistency with the existing pattern beats novelty
        // for a single new collection. Owned entities are loaded automatically with their owner
        // (no explicit .Include() needed), so EfGeneratedFileArchiveStore's existing plain
        // Add/ToListAsync/FirstOrDefaultAsync calls need no change to pick this up.
        builder.OwnsMany(r => r.Warnings, warnings =>
        {
            warnings.ToTable("GeneratedFileRecordWarnings");
            warnings.WithOwner().HasForeignKey("GeneratedFileRecordId");
            warnings.Property<int>("Id");
            warnings.HasKey("Id");

            warnings.Property(w => w.Sheet).IsRequired().HasMaxLength(200);
            warnings.Property(w => w.BlockIdentifier).IsRequired().HasMaxLength(500);
            warnings.Property(w => w.Code).IsRequired().HasMaxLength(100);
            warnings.Property(w => w.Message).IsRequired().HasMaxLength(1000);
            warnings.Property(w => w.ExtractedValue).HasMaxLength(500);
        });
    }
}
