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

        // Max length mirrors ImportProfile.MaxNameLength (Domain constructor is the source of truth
        // for the invariant; this column length just matches it). Unique index enforces name
        // uniqueness at the DB level as a defense-in-depth/race-condition safety net -- the primary
        // uniqueness check lives in EfImportProfileStore.SaveAsync (see ProfileNameAlreadyExistsException),
        // not here. Relies on the target database's default collation being case-insensitive
        // (SQL Server's usual default, e.g. SQL_Latin1_General_CP1_CI_AS) to match the Store's
        // OrdinalIgnoreCase comparison -- no explicit collation is set here since every environment
        // this project targets uses that default; revisit if a target database is ever provisioned
        // with a case-sensitive collation.
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(ImportProfile.MaxNameLength);

        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.Property(p => p.ReperePrefix)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.EquipementTypeElementNom)
            .IsRequired()
            .HasMaxLength(200);

        // DefaultTableaux/DefaultApplicationNames are plain lists of strings, not related entities --
        // mapped as EF Core primitive collections (JSON columns on SqlServer), same treatment as
        // UnconditionalColonneNames below.
        builder.Property(p => p.DefaultTableaux)
            .IsRequired();

        builder.Property(p => p.DefaultApplicationNames)
            .IsRequired();

        // Lot 067: TacheMultipleTypeLabel is a Code+Label pair, not a primitive scalar -- unlike
        // DefaultTableaux/DefaultApplicationNames above, this needs a real owned-entity mapping (same
        // shadow-Id-key convention as SheetRules below), not an EF Core primitive collection.
        builder.OwnsMany(p => p.TacheMultipleTypeLabels, labels =>
        {
            labels.ToTable("ImportProfileTacheMultipleTypeLabels");
            labels.WithOwner().HasForeignKey("ImportProfileId");
            labels.Property<int>("Id");
            labels.HasKey("Id");

            labels.Property(l => l.Code)
                .IsRequired()
                .HasMaxLength(ImportProfile.MaxListItemNameLength);

            labels.Property(l => l.Label)
                .IsRequired()
                .HasMaxLength(ImportProfile.MaxListItemNameLength);
        });

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

            // Lot 063: optional -- null for every sheet other than ISOLEMENT, and for any ISOLEMENT
            // rule predating this lot.
            rules.Property(r => r.ZeroEnergieExpectedValue)
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

            // HeaderFields/HeaderComposites (Lot 047,
            // docs/reference/spec-migration-entetes-profile-driven-directcell.md §3): the flat,
            // non-recursive header-rule model that replaces the hardcoded M2:O2/P2:Q2/R2:T2/N6
            // coordinates previously baked into ProcedureExtractionService/AutresJointsTouches/
            // DiversExtractionService. DirectCell is table-split (OwnsOne) into the same row as its
            // owning HeaderFieldRule, same treatment as RepeatingBlockLocator above.
            rules.OwnsMany(r => r.HeaderFields, headerFields =>
            {
                headerFields.ToTable("ImportProfileSheetRuleHeaderFields");
                headerFields.WithOwner().HasForeignKey("SheetExtractionRuleId");
                headerFields.Property<int>("Id");
                headerFields.HasKey("Id");

                headerFields.Property(f => f.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                headerFields.Property(f => f.StripReperePrefix)
                    .IsRequired();

                headerFields.Property(f => f.DateFormat)
                    .HasMaxLength(50);

                headerFields.OwnsOne(f => f.Cell, cell =>
                {
                    cell.Property(c => c.Sheet)
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnName("CellSheet");

                    cell.Property(c => c.Range)
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnName("CellRange");
                });

                headerFields.Navigation(f => f.Cell).IsRequired();
            });

            rules.OwnsMany(r => r.HeaderComposites, headerComposites =>
            {
                headerComposites.ToTable("ImportProfileSheetRuleHeaderComposites");
                headerComposites.WithOwner().HasForeignKey("SheetExtractionRuleId");
                headerComposites.Property<int>("Id");
                headerComposites.HasKey("Id");

                headerComposites.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                headerComposites.Property(c => c.Template)
                    .IsRequired()
                    .HasMaxLength(500);
            });

            // FieldPresencePointRules (PLATINES client feedback, 2026-09): each rule reuses
            // BlockFieldDefinition purely as "where to read this cell" (see the Domain type's own
            // comment), table-split (OwnsOne) into the same row exactly like HeaderFieldRule.Cell
            // above -- except this Cell is a BlockFieldDefinition, not a DirectCell, so it has the
            // same 4 columns as ImportProfileSheetRuleBlockFields above (Name/ColumnRange/
            // RowOffsetStart/RowOffsetEnd), not Sheet/Range.
            rules.OwnsMany(r => r.FieldPresencePointRules, fieldPresenceRules =>
            {
                fieldPresenceRules.ToTable("ImportProfileSheetRuleFieldPresencePointRules");
                fieldPresenceRules.WithOwner().HasForeignKey("SheetExtractionRuleId");
                fieldPresenceRules.Property<int>("Id");
                fieldPresenceRules.HasKey("Id");

                fieldPresenceRules.Property(r => r.ColonneName)
                    .IsRequired()
                    .HasMaxLength(200);

                fieldPresenceRules.OwnsOne(r => r.Cell, cell =>
                {
                    cell.Property(c => c.Name)
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnName("CellName");

                    cell.Property(c => c.ColumnRange)
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnName("CellColumnRange");

                    cell.Property(c => c.RowOffsetStart)
                        .IsRequired()
                        .HasColumnName("CellRowOffsetStart");

                    cell.Property(c => c.RowOffsetEnd)
                        .IsRequired()
                        .HasColumnName("CellRowOffsetEnd");
                });

                fieldPresenceRules.Navigation(r => r.Cell).IsRequired();
            });

            rules.Navigation(r => r.Locator).IsRequired();
        });
    }
}
