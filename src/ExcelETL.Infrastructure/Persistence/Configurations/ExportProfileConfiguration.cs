using ExcelETL.Domain.Generation.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class ExportProfileConfiguration : IEntityTypeConfiguration<ExportProfile>
{
    public void Configure(EntityTypeBuilder<ExportProfile> builder)
    {
        builder.ToTable("ExportProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        // SheetGenerationRule has no identity of its own -- owned by ExportProfile, not a sibling
        // entity. Same shadow "Id" key convention as ImportProfileConfiguration's SheetRules.
        builder.OwnsMany(p => p.SheetRules, rules =>
        {
            rules.ToTable("ExportProfileSheetRules");
            rules.WithOwner().HasForeignKey("ExportProfileId");
            rules.Property<int>("Id");
            rules.HasKey("Id");

            rules.Property(r => r.SheetName)
                .IsRequired()
                .HasMaxLength(200);

            rules.Property(r => r.PivotSource)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            rules.OwnsMany(r => r.ColumnDefinitions, columns =>
            {
                columns.ToTable("ExportProfileSheetRuleColumnDefinitions");
                columns.WithOwner().HasForeignKey("SheetGenerationRuleId");
                columns.Property<int>("Id");
                columns.HasKey("Id");

                columns.Property(c => c.Header)
                    .IsRequired()
                    .HasMaxLength(200);

                // Nullable by design (ColumnDefinition.Source = null is a legitimate, meaningful
                // state -- "no extraction rule wired to this column yet"), so no IsRequired() here.
                // Must persist and reread as a genuine null, not a default enum value.
                columns.Property(c => c.Source)
                    .HasConversion<string>()
                    .HasMaxLength(50);
            });

            rules.OwnsMany(r => r.PointColumnDefinitions, points =>
            {
                points.ToTable("ExportProfileSheetRulePointColumnDefinitions");
                points.WithOwner().HasForeignKey("SheetGenerationRuleId");
                points.Property<int>("Id");
                points.HasKey("Id");

                points.Property(p => p.ColonneNom)
                    .IsRequired()
                    .HasMaxLength(200);

                points.Property(p => p.Header)
                    .IsRequired()
                    .HasMaxLength(200);

                points.Property(p => p.MarkValue)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            rules.OwnsMany(r => r.ApplicationColumnDefinitions, applications =>
            {
                applications.ToTable("ExportProfileSheetRuleApplicationColumnDefinitions");
                applications.WithOwner().HasForeignKey("SheetGenerationRuleId");
                applications.Property<int>("Id");
                applications.HasKey("Id");

                applications.Property(a => a.ApplicationNom)
                    .IsRequired()
                    .HasMaxLength(200);

                applications.Property(a => a.Header)
                    .IsRequired()
                    .HasMaxLength(200);

                applications.Property(a => a.MarkValue)
                    .IsRequired()
                    .HasMaxLength(50);
            });
        });
    }
}
