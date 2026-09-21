using ExcelETL.BlazorAdmin.Editing;
using ExcelETL.BlazorAdmin.Editing.Import;
using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing.Import;

// Lot 075.2 (docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md). Pure xUnit, no
// bUnit -- the mapper has no dependency on Blazor rendering at all.
public class ImportProfileDraftMapperTests
{
    private static ImportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileDraftMapperTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId).GetAwaiter().GetResult()!;
    }

    // Every optional shape the seeded profile doesn't carry: a presence-only FieldPresencePointRule, and
    // non-default stored cell names that no input exposes.
    private static ImportProfile BuildHandBuiltProfile()
    {
        var locator = new RepeatingBlockLocator(
            "PLATINES", 17, 8, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("Designation", "H:U", -1, 0)]);
        var rule = new SheetExtractionRule(
            "PLATINES", locator,
            pointRules: [new ConditionalPointRule("Designation", ConditionOperator.NotEquals, "TUBING", "POSE")],
            unconditionalColonneNames: ["PROLOCK VANNES"],
            headerFields: [new HeaderFieldRule("nomMAD", new DirectCell("PLATINES", "M2:O2"), stripReperePrefix: true, dateFormat: "dd/MM/yyyy")],
            headerComposites: [new HeaderCompositeRule("Designation", "Rév {nomMAD}")],
            zeroEnergieExpectedValue: "ZERO ENERGIE",
            fieldPresencePointRules:
            [
                new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD"),
                new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL", "DEBUT REL"),
            ],
            couleurEtiquetteCell: new BlockFieldDefinition("CelluleCouleurPerso", "H:N", 1, 1),
            defaultCouleurEtiquette: "BLEUE",
            allowedCouleursEtiquette: ["ROUGE", "BLANC"]);
        return new ImportProfile(
            "Profil fait main", "MAD-OXO-", "MAD TRAVAUX", ["TRAVAUX COMPLET"], ["PROGRESS"], [rule],
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD")]);
    }

    private static ImportProfileDraft MinimalValidDraft() => new()
    {
        Name = "Nouveau profil",
        EquipementTypeElementNom = "MAD TRAVAUX",
        SheetRules = [MinimalValidRuleDraft()],
    };

    private static SheetExtractionRuleDraft MinimalValidRuleDraft() => new()
    {
        SheetName = "ISOLEMENT",
        FirstBlockStartRow = 19,
        Step = 7,
        StopFieldName = "Identification",
        Fields = [new BlockFieldDefinitionDraft { Name = "Identification", AbsoluteRange = "B19:E20" }],
    };

    private static void ShouldBeEquivalentProfile(ImportProfile actual, ImportProfile expected) =>
        actual.Should().BeEquivalentTo(expected, options => options
            .ComparingByMembers<ImportProfile>()
            .ComparingByMembers<SheetExtractionRule>()
            .ComparingRecordsByMembers()
            .WithStrictOrdering());

    // ------------------------------------------------------------------------------------------
    // Round trip -- the definitive guard-rail against defect B on the import side.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_SeededDefaultProfile_FromDomainThenToDomain_ProducesAnEquivalentProfile()
    {
        var original = LoadSeededDefaultProfile();

        var result = ImportProfileDraftMapper.ToDomain(ImportProfileDraftMapper.FromDomain(original));

        result.IsSuccess.Should().BeTrue();
        ShouldBeEquivalentProfile(result.Value!, original);
        result.Value!.Id.Should().Be(original.Id);
    }

    [Fact]
    public void RoundTrip_HandBuiltProfile_PreservesNullExpectedValueAndStoredCellNames()
    {
        var original = BuildHandBuiltProfile();

        var result = ImportProfileDraftMapper.ToDomain(ImportProfileDraftMapper.FromDomain(original));

        result.IsSuccess.Should().BeTrue();
        ShouldBeEquivalentProfile(result.Value!, original);
    }

    [Fact]
    public void FromDomain_BuildsAbsoluteRangesFromTheRuleStartRow()
    {
        var draft = ImportProfileDraftMapper.FromDomain(BuildHandBuiltProfile());

        var rule = draft.SheetRules.Single();
        rule.Fields.Select(f => f.AbsoluteRange).Should().Equal("B17:E18", "H16:U17");
        rule.CouleurEtiquetteCellRange.Should().Be("H18:N18");
        rule.CouleurEtiquetteCellName.Should().Be("CelluleCouleurPerso");
        rule.AllowedCouleursEtiquette.Should().Be("ROUGE, BLANC");
        rule.FieldPresencePointRules[0].ExpectedValue.Should().BeEmpty();
        rule.FieldPresencePointRules[0].CellName.Should().Be("PoseeLe");
    }

    [Fact]
    public void ToDomain_DraftWithNoId_BuildsANewProfile_WithTheDraftReperePrefix()
    {
        var draft = MinimalValidDraft();
        draft.ReperePrefix = "MAD-OXO-";

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value.ReperePrefix.Should().Be("MAD-OXO-");
    }

    // ------------------------------------------------------------------------------------------
    // Decision Q3 -- the absolute range is the source of truth.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ToDomain_FirstBlockStartRowChanged_KeepsTheAbsoluteRangesAndRecomputesTheOffsets()
    {
        var draft = ImportProfileDraftMapper.FromDomain(BuildHandBuiltProfile());
        var ruleDraft = draft.SheetRules.Single();
        ruleDraft.FirstBlockStartRow = 18;

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        ruleDraft.Fields[0].AbsoluteRange.Should().Be("B17:E18");
        var identification = result.Value!.SheetRules.Single().Locator.Fields[0];
        identification.RowOffsetStart.Should().Be(-1);
        identification.RowOffsetEnd.Should().Be(0);
    }

    [Fact]
    public void ConvertFieldPresencePointRule_Success_TrimsTheExpectedValueInTheDraft()
    {
        var ruleDraft = new FieldPresencePointRuleDraft { ColonneName = "X", AbsoluteRange = "H21:N21", ExpectedValue = " DEBUT MAD " };

        ImportProfileDraftMapper.ConvertFieldPresencePointRule(ruleDraft, 19).Value!.ExpectedValue.Should().Be("DEBUT MAD");
        ruleDraft.ExpectedValue.Should().Be("DEBUT MAD");
    }

    [Fact]
    public void ConvertField_Success_NormalizesTheTypedRange()
    {
        var fieldDraft = new BlockFieldDefinitionDraft { Name = "Identification", AbsoluteRange = "b19:b19" };

        var result = ImportProfileDraftMapper.ConvertField(fieldDraft, 19);

        result.IsSuccess.Should().BeTrue();
        fieldDraft.AbsoluteRange.Should().Be("B19");
    }

    // ------------------------------------------------------------------------------------------
    // Errors attached to the offending draft.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ConvertSheetRule_FieldWithUnparsableRange_AttachesADraftValidationExceptionToThatField()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.Fields[0].AbsoluteRange = "ZZZ";

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.Draft.Should().BeSameAs(ruleDraft.Fields[0]);
        error.Exception.Should().BeOfType<DraftValidationException>()
            .Which.ResourceKey.Should().Be("ImportProfileEditor_InvalidExcelRangeError");
    }

    [Fact]
    public void ConvertSheetRule_HeaderFieldWithEmptyName_AttachesTheErrorToThatHeaderField()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.HeaderFields.Add(new HeaderFieldRuleDraft { Name = "", Range = "M2:O2" });

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(ruleDraft.HeaderFields[0]);
    }

    [Fact]
    public void ConvertSheetRule_CompositeReferencingAnUnknownField_AttachesTheErrorToTheRule()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.HeaderComposites.Add(new HeaderCompositeRuleDraft { Name = "Designation", Template = "{inconnu}" });

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Draft.Should().BeSameAs(ruleDraft);
        error.Exception.Should().BeOfType<DomainRuleViolationException>();
    }

    [Fact]
    public void ConvertSheetRule_UnparsableCouleurEtiquetteCell_AttachesADraftValidationExceptionToTheRule()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.CouleurEtiquetteCellRange = "pas une plage";

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Draft.Should().BeSameAs(ruleDraft);
        error.Exception.Should().BeOfType<DraftValidationException>();
    }

    [Fact]
    public void ConvertSheetRule_NonPositiveStep_AttachesTheErrorToTheRule()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.Step = 0;

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(ruleDraft);
    }

    [Fact]
    public void ConvertSheetRule_UnconditionalColonneBlank_AttachesTheEmptyColonneNameError()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.UnconditionalColonneNames.Add(new StringItemDraft { Value = "  " });

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Draft.Should().BeSameAs(ruleDraft.UnconditionalColonneNames[0]);
        error.Exception.Should().BeOfType<DraftValidationException>()
            .Which.ResourceKey.Should().Be("ImportProfileEditor_EmptyColonneNameError");
    }

    [Fact]
    public void ToDomain_DuplicateTacheMultipleTypeLabelCode_AttachesTheErrorToTheSecondOccurrence()
    {
        var draft = MinimalValidDraft();
        draft.TacheMultipleTypeLabels =
        [
            new TacheMultipleTypeLabelDraft { Code = "TM_PROC_MAD", Label = "A" },
            new TacheMultipleTypeLabelDraft { Code = "tm_proc_mad", Label = "B" },
        ];

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(draft.TacheMultipleTypeLabels[1]);
    }

    [Fact]
    public void ToDomain_SeveralInvalidItems_ReportsEveryError()
    {
        var draft = MinimalValidDraft();
        draft.DefaultTableaux = [new StringItemDraft { Value = "" }];
        var ruleDraft = draft.SheetRules[0];
        ruleDraft.Fields[0].AbsoluteRange = "ZZZ";
        ruleDraft.PointRules.Add(new ConditionalPointRuleDraft { ColonneName = "POSE" });

        var result = ImportProfileDraftMapper.ToDomain(draft);

        var offendingDrafts = result.Errors.Select(e => e.Draft).ToList();
        offendingDrafts.Should().HaveCount(3);
        offendingDrafts.Should().Contain(d => ReferenceEquals(d, draft.DefaultTableaux[0]));
        offendingDrafts.Should().Contain(d => ReferenceEquals(d, ruleDraft.Fields[0]));
        offendingDrafts.Should().Contain(d => ReferenceEquals(d, ruleDraft.PointRules[0]));
    }

    [Fact]
    public void ToDomain_EmptyName_AttachesTheErrorToTheRootDraft()
    {
        var draft = MinimalValidDraft();
        draft.Name = "";

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(draft);
    }

    [Fact]
    public void ConvertDefaultTableau_ExcludesTheItemItselfFromTheDuplicateCheck_AndTrimsOnSuccess()
    {
        var item = new StringItemDraft { Value = " TRAVAUX COMPLET " };
        var other = new StringItemDraft { Value = "TRAVAUX DETAIL" };

        var result = ImportProfileDraftMapper.ConvertDefaultTableau(item, [other]);

        result.IsSuccess.Should().BeTrue();
        item.Value.Should().Be("TRAVAUX COMPLET");
    }

    [Fact]
    public void ConvertDefaultApplicationName_DuplicateOfAnother_Fails()
    {
        var item = new StringItemDraft { Value = "progress" };

        var result = ImportProfileDraftMapper.ConvertDefaultApplicationName(item, [new StringItemDraft { Value = "PROGRESS" }]);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(item);
    }

    // ------------------------------------------------------------------------------------------
    // Pending rows (the always-present "Add a ..." row of each list).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ToDomain_AllPendingRowsPristine_AreIgnored()
    {
        var draft = MinimalValidDraft();

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SheetRules.Should().ContainSingle();
        result.Value.DefaultTableaux.Should().BeEmpty();
        result.Value.TacheMultipleTypeLabels.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_ValidPendingRootRows_ArePromotedAtTheEndOfTheirLists()
    {
        var draft = MinimalValidDraft();
        draft.DefaultTableaux = [new StringItemDraft { Value = "TRAVAUX COMPLET" }];
        draft.PendingDefaultTableau.Value = "TRAVAUX DETAIL";
        draft.PendingDefaultApplicationName.Value = "PROGRESS";
        draft.PendingTacheMultipleTypeLabel.Code = "TM_PROC_MAD";
        draft.PendingTacheMultipleTypeLabel.Label = "Procédure MAD";

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultTableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        result.Value.DefaultApplicationNames.Should().Equal("PROGRESS");
        result.Value.TacheMultipleTypeLabels.Should().Equal(new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"));
        draft.DefaultTableaux.Select(t => t.Value).Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        DraftJson.IsPristine(draft.PendingDefaultTableau).Should().BeTrue();
        DraftJson.IsPristine(draft.PendingTacheMultipleTypeLabel).Should().BeTrue();
    }

    [Fact]
    public void ToDomain_ValidPendingSheetRule_IsPromoted()
    {
        var draft = MinimalValidDraft();
        draft.PendingSheetRule = MinimalValidRuleDraft();
        draft.PendingSheetRule.SheetName = "PLATINES";

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.Value!.SheetRules.Select(r => r.SheetName).Should().Equal("ISOLEMENT", "PLATINES");
        draft.SheetRules.Should().HaveCount(2);
        DraftJson.IsPristine(draft.PendingSheetRule).Should().BeTrue();
    }

    [Fact]
    public void ConvertSheetRule_ValidPendingNestedRows_ArePromotedAtTheEndOfTheirLists()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.PendingField.Name = "Designation";
        ruleDraft.PendingField.AbsoluteRange = "H18:U19";
        ruleDraft.PendingUnconditionalColonneName.Value = "PROLOCK VANNES";
        ruleDraft.PendingPointRule.ColonneName = "POSE";
        ruleDraft.PendingPointRule.SourceFieldName = "Designation";
        ruleDraft.PendingPointRule.ComparisonValue = "TUBING";
        ruleDraft.PendingFieldPresencePointRule.ColonneName = "RECEPTION DEBUT MAD";
        ruleDraft.PendingFieldPresencePointRule.AbsoluteRange = "H21:N21";
        ruleDraft.PendingHeaderField.Name = "nomMAD";
        ruleDraft.PendingHeaderField.Range = "M2:O2";
        ruleDraft.PendingHeaderComposite.Name = "Designation";
        ruleDraft.PendingHeaderComposite.Template = "Rév {nomMAD}";

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeTrue();
        var rule = result.Value!;
        rule.Locator.Fields.Select(f => f.Name).Should().Equal("Identification", "Designation");
        rule.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES");
        rule.PointRules.Should().ContainSingle();
        rule.FieldPresencePointRules.Single().Cell.Name.Should().Be(ImportProfileDraftMapper.DefaultFieldPresenceCellName);
        rule.HeaderFields.Single().Cell.Sheet.Should().Be("ISOLEMENT");
        rule.HeaderComposites.Should().ContainSingle();
        ruleDraft.Fields.Should().HaveCount(2);
        DraftJson.IsPristine(ruleDraft.PendingField).Should().BeTrue();
        DraftJson.IsPristine(ruleDraft.PendingHeaderComposite).Should().BeTrue();
    }

    [Fact]
    public void ConvertSheetRule_InvalidPendingPointRule_BlocksTheConversion_ErrorOnThePendingRow()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.PendingPointRule.ColonneName = "POSE";

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(ruleDraft.PendingPointRule);
        ruleDraft.PointRules.Should().BeEmpty();
        ruleDraft.PendingPointRule.ColonneName.Should().Be("POSE");
    }

    [Fact]
    public void ConvertSheetRule_BlankPendingUnconditionalColonne_IsIgnored_LikeTheAddButton()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.PendingUnconditionalColonneName.Value = "   ";

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UnconditionalColonneNames.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_InvalidPendingRootRow_BlocksTheConversion_ErrorOnThePendingRow()
    {
        var draft = MinimalValidDraft();
        draft.PendingTacheMultipleTypeLabel.Code = "TM_PROC_MAD";

        var result = ImportProfileDraftMapper.ToDomain(draft);

        result.Errors.Should().ContainSingle().Which.Draft.Should().BeSameAs(draft.PendingTacheMultipleTypeLabel);
    }

    // ------------------------------------------------------------------------------------------
    // Preserved behaviors of the pre-draft forms.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ConvertSheetRule_HeaderFieldCellSheet_IsAlwaysTheRuleSheetName()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.HeaderFields.Add(new HeaderFieldRuleDraft { Name = "nomMAD", Range = "M2:O2" });

        var result = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft);

        result.Value!.HeaderFields.Single().Cell.Sheet.Should().Be("ISOLEMENT");
    }

    [Fact]
    public void ConvertSheetRule_BlankOptionalScalars_BecomeNull()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.ZeroEnergieExpectedValue = " ";
        ruleDraft.DefaultCouleurEtiquette = "";
        ruleDraft.CouleurEtiquetteCellRange = " ";
        ruleDraft.AllowedCouleursEtiquette = " , ";
        ruleDraft.HeaderFields.Add(new HeaderFieldRuleDraft { Name = "nomMAD", Range = "M2:O2", DateFormat = " " });
        ruleDraft.FieldPresencePointRules.Add(new FieldPresencePointRuleDraft { ColonneName = "X", AbsoluteRange = "H21:N21", ExpectedValue = " " });

        var rule = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft).Value!;

        rule.ZeroEnergieExpectedValue.Should().BeNull();
        rule.DefaultCouleurEtiquette.Should().BeNull();
        rule.CouleurEtiquetteCell.Should().BeNull();
        rule.HeaderFields.Single().DateFormat.Should().BeNull();
        rule.FieldPresencePointRules.Single().ExpectedValue.Should().BeNull();
    }

    [Fact]
    public void ConvertSheetRule_AllowedCouleursEtiquette_IsSplitOnCommasAndTrimmed_BlankBecomesNull()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.AllowedCouleursEtiquette = " ROUGE ,BLANC,, ";

        ImportProfileDraftMapper.ConvertSheetRule(ruleDraft).Value!.AllowedCouleursEtiquette
            .Should().Equal("ROUGE", "BLANC");
        ruleDraft.AllowedCouleursEtiquette.Should().Be("ROUGE, BLANC");

        ruleDraft.AllowedCouleursEtiquette = "";
        ImportProfileDraftMapper.ConvertSheetRule(ruleDraft).Value!.AllowedCouleursEtiquette.Should().BeNull();
    }

    [Fact]
    public void ConvertSheetRule_NewCouleurEtiquetteCell_GetsTheDefaultCellName()
    {
        var ruleDraft = MinimalValidRuleDraft();
        ruleDraft.CouleurEtiquetteCellRange = "H20:N20";

        var cell = ImportProfileDraftMapper.ConvertSheetRule(ruleDraft).Value!.CouleurEtiquetteCell!;

        cell.Name.Should().Be(ImportProfileDraftMapper.DefaultCouleurEtiquetteCellName);
        cell.RowOffsetStart.Should().Be(1);
    }
}
