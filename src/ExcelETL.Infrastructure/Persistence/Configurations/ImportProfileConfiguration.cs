using ExcelETL.Domain.Extraction.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelETL.Infrastructure.Persistence.Configurations;

public class ImportProfileConfiguration : IEntityTypeConfiguration<ImportProfile>
{
    public void Configure(EntityTypeBuilder<ImportProfile> builder)
    {
        builder.ToTable("ImportProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.ReperePrefix)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.EquipementTypeElementNom)
            .IsRequired()
            .HasMaxLength(200);

        // SheetExtractionRule has no identity of its own (see the Domain type's own comment) -- it's
        // owned by ImportProfile, not a sibling entity like SheetConfig/CellMapping. A shadow "Id" key
        // is required because EF Core owned collections must have a key and SheetExtractionRule
        // exposes no natural candidate.
        builder.OwnsMany(p => p.SheetRules, rules =>
        {
            rules.ToTable("ImportProfileSheetRules");
            rules.WithOwner().HasForeignKey("ImportProfileId");
            rules.Property<int>("Id");
            rules.HasKey("Id");

            rules.Property(r => r.SheetName)
                .IsRequired()
                .HasMaxLength(200);

            // UnconditionalColonneNames is a plain list of strings, not a related entity -- mapped as
            // an EF Core primitive collection (JSON column on SqlServer) rather than a second owned
            // entity type, since there is nothing beyond the string itself to model.
            rules.Property(r => r.UnconditionalColonneNames)
                .IsRequired();

            rules.OwnsOne(r => r.Locator, locator =>
            {
                locator.Property(l => l.Sheet)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("LocatorSheet");

                locator.Property(l => l.FirstBlockStartRow)
                    .IsRequired()
                    .HasColumnName("LocatorFirstBlockStartRow");

                locator.Property(l => l.Step)
                    .IsRequired()
                    .HasColumnName("LocatorStep");

                locator.Property(l => l.StopFieldName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("LocatorStopFieldName");

                locator.OwnsMany(l => l.Fields, fields =>
                {
                    fields.ToTable("ImportProfileSheetRuleBlockFields");
                    fields.WithOwner().HasForeignKey("SheetExtractionRuleId");
                    fields.Property<int>("Id");
                    fields.HasKey("Id");

                    fields.Property(f => f.Name)
                        .IsRequired()
                        .HasMaxLength(200);

                    fields.Property(f => f.ColumnRange)
                        .IsRequired()
                        .HasMaxLength(20);

                    fields.Property(f => f.RowOffsetStart)
                        .IsRequired();

                    fields.Property(f => f.RowOffsetEnd)
                        .IsRequired();
                });
            });

            rules.OwnsMany(r => r.PointRules, pointRules =>
            {
                pointRules.ToTable("ImportProfileSheetRulePointRules");
                pointRules.WithOwner().HasForeignKey("SheetExtractionRuleId");
                pointRules.Property<int>("Id");
                pointRules.HasKey("Id");

                pointRules.Property(pr => pr.SourceFieldName)
                    .IsRequired()
                    .HasMaxLength(200);

                pointRules.Property(pr => pr.Operator)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);

                pointRules.Property(pr => pr.ComparisonValue)
                    .IsRequired()
                    .HasMaxLength(200);

                pointRules.Property(pr => pr.ColonneName)
                    .IsRequired()
                    .HasMaxLength(200);
            });

            rules.Navigation(r => r.Locator).IsRequired();
        });
    }
}
