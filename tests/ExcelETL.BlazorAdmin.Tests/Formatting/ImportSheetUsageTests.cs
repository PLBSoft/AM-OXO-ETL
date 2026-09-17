using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.BlazorAdmin.Shared;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.1 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportSheetUsageTests
{
    private const string Procedure = "PROCEDURE";
    private const string Isolement = "ISOLEMENT";
    private const string Platines = "PLATINES";
    private const string OrificesCapacites = "ORIFICES CAPACITES";
    private const string AutresJointsTouches = "AUTRES JOINTS TOUCHES";
    private const string Divers = "DIVERS";

    // D8570 rather than C7401 (named in the ticket): C7401's ORIFICES CAPACITES, AUTRES JOINTS TOUCHES
    // and DIVERS sheets produce no element at all, which would make the drift guard vacuous there.
    private const string FixtureFileName = "Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx";
    private const string TestColonneName = "COLONNE TEST 078";

    public static TheoryData<string, SheetRuleMember[]> ReadMembersBySheet => new()
    {
        { Procedure, [SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules] },
        {
            Isolement,
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.ConditionalPointRules,
                SheetRuleMember.ZeroEnergieExpectedValue
            ]
        },
        {
            Platines,
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.FieldPresencePointRules,
                SheetRuleMember.CouleurEtiquette
            ]
        },
        {
            OrificesCapacites,
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.FieldPresencePointRules,
                SheetRuleMember.CouleurEtiquette
            ]
        },
        {
            AutresJointsTouches,
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
                SheetRuleMember.ConditionalPointRules, SheetRuleMember.CouleurEtiquette
            ]
        },
        {
            Divers,
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
                SheetRuleMember.ConditionalPointRules
            ]
        },
    };

    [Theory]
    [MemberData(nameof(ReadMembersBySheet))]
    public void For_KnownSheet_IsProcessed_AndReadsExactlyTheMembersItsExtractionServiceReads(
        string sheetName, SheetRuleMember[] expectedMembers)
    {
        var usage = ImportSheetUsage.For(sheetName);

        usage.Should().NotBeNull();
        usage!.ReadMembers.Should().BeEquivalentTo(expectedMembers);
        ImportSheetUsage.IsProcessed(sheetName).Should().BeTrue();
    }

    [Fact]
    public void KnownSheetNames_AreTheSixPipelineSheets_InPipelineOrder() =>
        ImportSheetUsage.KnownSheetNames.Should().Equal(
            Procedure, Isolement, Platines, OrificesCapacites, AutresJointsTouches, Divers);

    [Theory]
    [InlineData("MA FEUILLE")]
    [InlineData("procedure")]
    [InlineData("PROCEDURE ")]
    public void For_UnknownOrDifferentlyCasedSheetName_IsNotProcessed(string sheetName)
    {
        ImportSheetUsage.For(sheetName).Should().BeNull();
        ImportSheetUsage.IsProcessed(sheetName).Should().BeFalse();
    }

    [Fact]
    public void For_Procedure_RequiredHeaderNames_CarryTheirBusinessRole()
    {
        var usage = ImportSheetUsage.For(Procedure)!;

        usage.RequiredHeaderFields.Should().Equal(
            new RequiredHeaderName(ProcedureHeaderFieldNames.NomMad, HeaderRole.EquipementRepere),
            new RequiredHeaderName(ProcedureHeaderFieldNames.Revision, HeaderRole.Revision),
            new RequiredHeaderName(ProcedureHeaderFieldNames.DateRev, HeaderRole.RevisionDate));
        usage.RequiredHeaderComposites.Should().Equal(
            new RequiredHeaderName(ProcedureHeaderFieldNames.Designation, HeaderRole.EquipementDesignation));
    }

    [Theory]
    [InlineData(AutresJointsTouches)]
    [InlineData(Divers)]
    public void For_SheetsEchoingTheEquipementRepere_RequireRepereEcho(string sheetName)
    {
        var usage = ImportSheetUsage.For(sheetName)!;

        usage.RequiredHeaderFields.Should().Equal(
            new RequiredHeaderName(SharedHeaderFieldNames.RepereEcho, HeaderRole.RepereEcho));
        usage.RequiredHeaderComposites.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Procedure)]
    [InlineData(Isolement)]
    [InlineData(Platines)]
    [InlineData(OrificesCapacites)]
    [InlineData(AutresJointsTouches)]
    [InlineData(Divers)]
    [InlineData("MA FEUILLE")]
    public void RequiredHeaderNames_MatchKnownHeaderFieldNames(string sheetName)
    {
        var usage = ImportSheetUsage.For(sheetName);
        var known = KnownHeaderFieldNames.For(sheetName);

        (usage?.RequiredHeaderFields.Select(h => h.Name) ?? []).Should().Equal(known.Fields);
        (usage?.RequiredHeaderComposites.Select(h => h.Name) ?? []).Should().Equal(known.Composites);
    }

    // Drift guard: a member marked "not read" must leave the real pipeline's output unchanged. If an
    // extraction service starts reading it one day, this fails before the Details page misdescribes it.
    public static TheoryData<string, SheetRuleMember> NotReadCases()
    {
        var cases = new TheoryData<string, SheetRuleMember>();
        foreach (var sheetName in ImportSheetUsage.KnownSheetNames)
        {
            foreach (var member in MutableMembers.Where(m => !ReadMembersFor(sheetName).Contains(m)))
            {
                cases.Add(sheetName, member);
            }
        }

        return cases;
    }

    // Counterpart proving the mutations below are not no-ops: applied to a sheet that does read the
    // member, they change the output. HeaderRules/ZeroEnergieExpectedValue have no such cheap proof on
    // this fixture (adding a header field changes nothing; no ISOLEMENT zero-energie cell is filled).
    public static TheoryData<string, SheetRuleMember> ReadCases() => new()
    {
        { Isolement, SheetRuleMember.UnconditionalColonnes },
        { Divers, SheetRuleMember.UnconditionalColonnes },
        { Isolement, SheetRuleMember.ConditionalPointRules },
        { AutresJointsTouches, SheetRuleMember.ConditionalPointRules },
        { Platines, SheetRuleMember.FieldPresencePointRules },
        { AutresJointsTouches, SheetRuleMember.CouleurEtiquette },
    };

    [Theory]
    [MemberData(nameof(NotReadCases))]
    public void NotReadMember_WhenFilledIn_DoesNotChangeTheRealPipelineOutput(string sheetName, SheetRuleMember member)
    {
        var profile = LoadSeededDefaultProfile();

        var baseline = RunPipeline(profile);
        var mutated = RunPipeline(WithMemberFilledIn(profile, sheetName, member));

        mutated.Isolements.Should().BeEquivalentTo(baseline.Isolements, o => o.WithStrictOrdering());
        mutated.Points.Should().BeEquivalentTo(baseline.Points, o => o.WithStrictOrdering());
        mutated.TachesMultiples.Should().BeEquivalentTo(baseline.TachesMultiples, o => o.WithStrictOrdering());
        mutated.Errors.Should().BeEquivalentTo(baseline.Errors, o => o.WithStrictOrdering());
    }

    [Theory]
    [MemberData(nameof(ReadCases))]
    public void ReadMember_WhenFilledIn_ChangesTheRealPipelineOutput(string sheetName, SheetRuleMember member)
    {
        ReadMembersFor(sheetName).Should().Contain(member);
        var profile = LoadSeededDefaultProfile();

        var baseline = RunPipeline(profile);
        var mutated = RunPipeline(WithMemberFilledIn(profile, sheetName, member));

        var unchanged =
            mutated.Isolements.SequenceEqual(baseline.Isolements) && mutated.Points.SequenceEqual(baseline.Points);
        unchanged.Should().BeFalse();
    }

    private static readonly SheetRuleMember[] MutableMembers =
    [
        SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.ConditionalPointRules,
        SheetRuleMember.FieldPresencePointRules, SheetRuleMember.ZeroEnergieExpectedValue, SheetRuleMember.CouleurEtiquette
    ];

    // Independent copy of the expected table, so the drift guard doesn't just trust the production table.
    private static SheetRuleMember[] ReadMembersFor(string sheetName) =>
        ReadMembersBySheet.Single(row => (string)row[0] == sheetName)[1] as SheetRuleMember[] ?? [];

    private static SheetExtractionRule WithMemberFilledIn(SheetExtractionRule rule, SheetRuleMember member) =>
        new(
            rule.SheetName,
            rule.Locator,
            member == SheetRuleMember.ConditionalPointRules
                ? [.. rule.PointRules, new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "__JAMAIS__", TestColonneName)]
                : rule.PointRules,
            member == SheetRuleMember.UnconditionalColonnes
                ? [.. rule.UnconditionalColonneNames, TestColonneName]
                : rule.UnconditionalColonneNames,
            member == SheetRuleMember.HeaderRules
                ? [.. rule.HeaderFields, new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(rule.SheetName, "N6"))]
                : rule.HeaderFields,
            rule.HeaderComposites,
            member == SheetRuleMember.ZeroEnergieExpectedValue ? "VALEUR TEST 078" : rule.ZeroEnergieExpectedValue,
            member == SheetRuleMember.FieldPresencePointRules
                ? [.. rule.FieldPresencePointRules, new FieldPresencePointRule(FirstFieldOf(rule), TestColonneName)]
                : rule.FieldPresencePointRules,
            rule.CouleurEtiquetteCell,
            member == SheetRuleMember.CouleurEtiquette ? "VERT" : rule.DefaultCouleurEtiquette,
            rule.AllowedCouleursEtiquette);

    // Reads the stop field's own cell: always filled in for every extracted block.
    private static BlockFieldDefinition FirstFieldOf(SheetExtractionRule rule)
    {
        var stopField = rule.Locator.Fields.First(f => f.Name == rule.Locator.StopFieldName);
        return new BlockFieldDefinition("TestCell", stopField.ColumnRange, stopField.RowOffsetStart, stopField.RowOffsetEnd);
    }

    private static ImportProfile WithMemberFilledIn(ImportProfile profile, string sheetName, SheetRuleMember member) =>
        new(
            profile.Id, profile.Name, profile.ReperePrefix, profile.EquipementTypeElementNom,
            profile.DefaultTableaux, profile.DefaultApplicationNames,
            [.. profile.SheetRules.Select(rule => rule.SheetName == sheetName ? WithMemberFilledIn(rule, member) : rule)],
            profile.TacheMultipleTypeLabels);

    private static ImportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ImportSheetUsageTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId).GetAwaiter().GetResult()!;
    }

    private static ImportResult RunPipeline(ImportProfile profile)
    {
        var textTransformEvaluator = new TextTransformEvaluator();
        var conditionalPointRuleEvaluator = new ConditionalPointRuleEvaluator();
        var repeatingBlockReader = new RepeatingBlockReader();
        var headerRuleResolver = new HeaderRuleResolver(textTransformEvaluator);
        var orchestrator = new ImportPipelineOrchestrator(
            new ProcedureExtractionService(headerRuleResolver, NullLogger<ProcedureExtractionService>.Instance),
            new IsolementExtractionService(
                textTransformEvaluator, conditionalPointRuleEvaluator, NullLogger<IsolementExtractionService>.Instance),
            new UnconditionalIsolementSheetExtractionService(
                repeatingBlockReader, textTransformEvaluator, NullLogger<UnconditionalIsolementSheetExtractionService>.Instance),
            new AutresJointsTouchesExtractionService(
                repeatingBlockReader, textTransformEvaluator, conditionalPointRuleEvaluator, headerRuleResolver,
                NullLogger<AutresJointsTouchesExtractionService>.Instance),
            new DiversExtractionService(
                repeatingBlockReader, textTransformEvaluator, conditionalPointRuleEvaluator, headerRuleResolver,
                NullLogger<DiversExtractionService>.Instance),
            NullLogger<ImportPipelineOrchestrator>.Instance);

        using var stream = File.OpenRead(FixturePath(FixtureFileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return orchestrator.Run(workbookReader, profile);
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the tests/Fixtures directory.");
        }

        return Path.Combine(directory.FullName, "Fixtures", fileName);
    }
}
