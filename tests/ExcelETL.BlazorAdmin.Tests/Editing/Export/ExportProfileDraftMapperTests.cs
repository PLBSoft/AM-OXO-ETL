using ExcelETL.BlazorAdmin.Editing;
using ExcelETL.BlazorAdmin.Editing.Export;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing.Export;

// Lot 074.2 (docs/tickets/tickets-tdd-lot-074-pilote-brouillon-editeur-profil-export.md). Pure xUnit,
// no bUnit -- the mapper has no dependency on Blazor rendering at all.
public class ExportProfileDraftMapperTests
{
    private static ExportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileDraftMapperTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId).GetAwaiter().GetResult()!;
    }

    // ------------------------------------------------------------------------------------------
    // Round trip against the real seeded default profile -- the definitive guard-rail against
    // defect B ("champ perdu à la reconstruction") on the export side.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_SeededDefaultProfile_FromDomainThenToDomain_ProducesAnEquivalentProfile()
    {
        var original = LoadSeededDefaultProfile();

        var draft = ExportProfileDraftMapper.FromDomain(original);
        var result = ExportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(original, options => options
            .ComparingRecordsByMembers()
            .WithStrictOrdering());
    }

    [Fact]
    public void RoundTrip_SeededDefaultProfile_IdIsPreserved()
    {
        var original = LoadSeededDefaultProfile();

        var draft = ExportProfileDraftMapper.FromDomain(original);
        var result = ExportProfileDraftMapper.ToDomain(draft);

        result.Value!.Id.Should().Be(original.Id);
    }

    [Fact]
    public void ToDomain_DraftWithNoId_BuildsANewProfile_WithAFreshGuid()
    {
        var draft = new ExportProfileDraft
        {
            Id = null,
            Name = "Nouveau profil",
            SheetRules =
            [
                new SheetGenerationRuleDraft
                {
                    SheetName = "Parents",
                    PivotSource = PivotSource.Equipement,
                    Columns = [new ColumnDefinitionDraft { Header = "Repère", SourceValue = nameof(PivotFieldRef.EquipementRepere) }],
                },
            ],
        };

        var result = ExportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------------------------------------
    // Error attachment -- each failure must carry the offending draft, not just a message.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ConvertColumn_WithEmptyHeader_AttachesErrorToThisColumnDraft_NotTheRule()
    {
        var columnDraft = new ColumnDefinitionDraft { Header = string.Empty, SourceValue = string.Empty };

        var result = ExportProfileDraftMapper.ConvertColumn(columnDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, columnDraft));
    }

    [Fact]
    public void ConvertSheetRule_WithOneEmptyHeaderColumn_AttachesErrorToThatColumnDraft()
    {
        var columnDraft = new ColumnDefinitionDraft { Header = string.Empty, SourceValue = string.Empty };
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Parents",
            PivotSource = PivotSource.Equipement,
            Columns = [columnDraft],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, columnDraft));
    }

    [Fact]
    public void ConvertSheetRule_WithPointColumnUnderTacheMultiple_AttachesErrorToTheRuleDraft()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Tâches multiples",
            PivotSource = PivotSource.TacheMultiple,
            PointColumns = [new PointColumnDefinitionDraft { ColonneNom = "X", Header = "X" }],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, ruleDraft));
    }

    [Fact]
    public void ConvertSheetRule_WithDuplicateHeader_AttachesErrorToTheRuleDraft()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Parents",
            PivotSource = PivotSource.Equipement,
            Columns =
            [
                new ColumnDefinitionDraft { Header = "Repère", SourceValue = nameof(PivotFieldRef.EquipementRepere) },
                new ColumnDefinitionDraft { Header = "Repère", SourceValue = nameof(PivotFieldRef.EquipementDesignation) },
            ],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, ruleDraft));
    }

    [Fact]
    public void ConvertSheetRule_WithColumnSourceIncompatibleWithPivotSource_AttachesErrorToTheRuleDraft()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Parents",
            PivotSource = PivotSource.Equipement,
            Columns = [new ColumnDefinitionDraft { Header = "Numéro", SourceValue = nameof(PivotFieldRef.IsolementRepere) }],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, ruleDraft));
    }

    [Fact]
    public void ConvertSheetRule_WithSeveralInvalidElements_ReturnsEveryError_NotJustTheFirst()
    {
        var emptyColumn = new ColumnDefinitionDraft { Header = string.Empty };
        var emptyPointColumn = new PointColumnDefinitionDraft { ColonneNom = string.Empty, Header = "X" };
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Enfants",
            PivotSource = PivotSource.Isolement,
            Columns = [emptyColumn],
            PointColumns = [emptyPointColumn],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => ReferenceEquals(e.Draft, emptyColumn));
        result.Errors.Should().Contain(e => ReferenceEquals(e.Draft, emptyPointColumn));
    }

    // ------------------------------------------------------------------------------------------
    // Pending row handling (defect A -- see this file's own header comment for the invariant).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ConvertSheetRule_EmptyPendingColumn_IsIgnored_RuleStillConvertsSuccessfully()
    {
        var ruleDraft = new SheetGenerationRuleDraft { SheetName = "Parents", PivotSource = PivotSource.Equipement };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void ConvertSheetRule_NonEmptyValidPendingColumn_IsAddedToTheEndOfTheList_AndSlotIsReset()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Parents",
            PivotSource = PivotSource.Equipement,
            Columns = [new ColumnDefinitionDraft { Header = "Repère", SourceValue = nameof(PivotFieldRef.EquipementRepere) }],
            PendingColumn = new ColumnDefinitionDraft { Header = "Désignation", SourceValue = nameof(PivotFieldRef.EquipementDesignation) },
        };
        var originalPending = ruleDraft.PendingColumn;

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ColumnDefinitions.Should().HaveCount(2);
        result.Value!.ColumnDefinitions[1].Header.Should().Be("Désignation");

        // Same behavior as clicking "Add": the pending draft itself is moved into the list, and the
        // slot is replaced with a brand new instance.
        ruleDraft.Columns.Should().Contain(originalPending);
        ruleDraft.PendingColumn.Should().NotBeSameAs(originalPending);
        DraftJson.IsPristine(ruleDraft.PendingColumn).Should().BeTrue();
    }

    [Fact]
    public void ConvertSheetRule_NonEmptyInvalidPendingPointColumn_BlocksConversion_ErrorOnThePendingDraft()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Enfants",
            PivotSource = PivotSource.Isolement,
            PendingPointColumn = new PointColumnDefinitionDraft { ColonneNom = string.Empty, Header = "Travaux complet" },
        };
        var originalPending = ruleDraft.PendingPointColumn;

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => ReferenceEquals(e.Draft, originalPending));
        ruleDraft.PendingPointColumn.Should().BeSameAs(originalPending);
    }

    [Fact]
    public void ToDomain_NonEmptyValidPendingSheetRule_IsAddedToTheEndOfTheList()
    {
        var draft = new ExportProfileDraft
        {
            Name = "Profil export OXO",
            PendingSheetRule = new SheetGenerationRuleDraft
            {
                SheetName = "Enfants",
                PivotSource = PivotSource.Isolement,
                Columns = [new ColumnDefinitionDraft { Header = "Numéro", SourceValue = nameof(PivotFieldRef.IsolementRepere) }],
            },
        };

        var result = ExportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SheetRules.Should().ContainSingle(r => r.SheetName == "Enfants");
        draft.SheetRules.Should().ContainSingle(r => r.SheetName == "Enfants");
        DraftJson.IsPristine(draft.PendingSheetRule).Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------
    // ConstantColumns pass through unmodified (the exact bug fixed by commit 0cbac22).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ConvertSheetRule_WithConstantColumns_CarriesThemThroughUnmodified()
    {
        var ruleDraft = new SheetGenerationRuleDraft
        {
            SheetName = "Tâches multiples",
            PivotSource = PivotSource.TacheMultiple,
            Columns = [new ColumnDefinitionDraft { Header = "Action", SourceValue = nameof(PivotFieldRef.TacheMultipleAction) }],
            ConstantColumns = [new ConstantColumnDefinition("SUPPRESSION", "N")],
        };

        var result = ExportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ConstantColumnDefinitions.Should().ContainSingle(c => c.Header == "SUPPRESSION" && c.Value == "N");
    }
}
