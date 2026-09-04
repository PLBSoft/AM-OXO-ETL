using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Procedure;

public class ProcedureExtractionServiceTests
{
    private const string Sheet = "PROCEDURE";
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private static readonly string[] DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"];

    private readonly ProcedureExtractionService _sut =
        new(new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ProcedureExtractionService>.Instance);

    // Lot 047: PROCEDURE's header rules -- transcribed from the coordinates/template previously
    // hardcoded in ProcedureExtractionService (M2:O2/P2:Q2/R2:T2, "Rév {revision} du {dateRev}") so
    // this test's Mock<IWorkbookReader> cell keys (BaseHeaderCells) stay exactly as before the lot.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 1, ProcedureFieldNames.Action,
        [
            new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
        ]),
        [],
        [],
        [
            new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell(Sheet, "M2:O2"), stripReperePrefix: true),
            new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell(Sheet, "P2:Q2")),
            new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")
        ],
        [
            new HeaderCompositeRule(
                ProcedureHeaderFieldNames.Designation,
                $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
        ]);

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(Sheet, It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock;
    }

    private static Dictionary<string, string?> BaseHeaderCells(string dateRevision = "16/07/2026") => new()
    {
        ["M2:O2"] = "MAD-OXO-38-C7401",
        ["P2:Q2"] = "2",
        ["R2:T2"] = dateRevision
    };

    [Fact]
    public void Extract_WithValidHeaderAndNoTasks_ReturnsEquipementDesignationAndUnconditionalPoints()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("38-C7401");
        result.Equipement.Designation.Should().Be("Rév 2 du 16/07/2026");
        result.Equipement.TypeElementNom.Should().Be(EquipementTypeElementNom);
        result.Points.Should().BeEquivalentTo(
        [
            new PointPivot("TRAVAUX COMPLET", "38-C7401"),
            new PointPivot("TRAVAUX DETAIL", "38-C7401")
        ]);
        result.TachesMultiples.Should().BeEmpty();
        result.Isolements.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_BuildsUnconditionalPointsFromProfileDefaultTableaux_NotHardcodedConstants()
    {
        // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), U3: the 2 PROCEDURE Points
        // used to come from private consts -- same architecture guard-rail as
        // Extract_UsesEquipementTypeElementNomFromProfile_NotAHardcodedConstant above, now applied to
        // the Tableau names too.
        var cells = BaseHeaderCells();
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(
            workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, ["FOO", "BAR"]);

        result.Points.Should().BeEquivalentTo(
        [
            new PointPivot("FOO", "38-C7401"),
            new PointPivot("BAR", "38-C7401")
        ]);
    }

    [Fact]
    public void Extract_WithEmptyDefaultTableaux_ProducesNoPoints()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, []);

        result.Points.Should().BeEmpty();
    }

    [Theory]
    [InlineData("MAD TRAVAUX")]
    [InlineData("REL TRAVAUX")]
    public void Extract_UsesEquipementTypeElementNomFromProfile_NotAHardcodedConstant(string profileValue)
    {
        // Architecture guard-rail (ticket C1): the value must come from whatever the caller passes
        // in (i.e. the active ImportProfile), never a literal baked into the service.
        var cells = BaseHeaderCells();
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, profileValue, DefaultTableaux);

        result.Equipement!.TypeElementNom.Should().Be(profileValue);
    }

    [Fact]
    public void Extract_ReadsHeaderCellsFromWhicheverCoordinatesTheProfileDeclares_NotHardcodedConstants()
    {
        // Lot 047, 47.5 anti-hardcoding guard-rail (same pattern as
        // Extract_UsesEquipementTypeElementNomFromProfile_NotAHardcodedConstant above): a profile
        // with different header-cell coordinates than DefaultProfileSeeder's must be honored, never a
        // literal baked into the service.
        var sheetRule = new SheetExtractionRule(
            Sheet,
            new RepeatingBlockLocator(Sheet, 9, 1, ProcedureFieldNames.Action,
            [
                new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
            ]),
            [],
            [],
            [
                new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell(Sheet, "X1:Y1"), stripReperePrefix: true),
                new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell(Sheet, "X2")),
                new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell(Sheet, "X3"), dateFormat: "dd/MM/yyyy")
            ],
            [
                new HeaderCompositeRule(
                    ProcedureHeaderFieldNames.Designation,
                    $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
            ]);
        var cells = new Dictionary<string, string?>
        {
            ["X1:Y1"] = "MAD-OXO-99-OTHER",
            ["X2"] = "7",
            ["X3"] = "01/01/2020",
            ["C9:L9"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, sheetRule, ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement!.Repere.Should().Be("99-OTHER");
        result.Equipement.Designation.Should().Be("Rév 7 du 01/01/2020");
    }

    [Fact]
    public void Extract_WithDifferentDesignationTemplate_UsesTheProfilesTemplate_NotAHardcodedOne()
    {
        // Lot 047, 47.5: two profiles, two gabarits -> two different results.
        var sheetRule = new SheetExtractionRule(
            Sheet,
            new RepeatingBlockLocator(Sheet, 9, 1, ProcedureFieldNames.Action,
            [
                new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
            ]),
            [],
            [],
            [
                new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell(Sheet, "M2:O2"), stripReperePrefix: true),
                new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell(Sheet, "P2:Q2")),
                new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")
            ],
            [
                new HeaderCompositeRule(
                    ProcedureHeaderFieldNames.Designation,
                    $"Version {{{ProcedureHeaderFieldNames.Revision}}} ({{{ProcedureHeaderFieldNames.DateRev}}})")
            ]);
        var cells = BaseHeaderCells();
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, sheetRule, ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement!.Designation.Should().Be("Version 2 (16/07/2026)");
    }

    [Fact]
    public void Extract_WithNormalTaskRow_SetsOrdreAndEstFaticeFalse()
    {
        var cells = BaseHeaderCells();
        cells["B9"] = "1";
        cells["C9:L9"] = "Some action";
        cells["M9:N9"] = "Acteur1";
        cells["O9:Q9"] = "RisqueX";
        cells["R9"] = "MAD";
        cells["T9:U9"] = "16/07/2026";
        cells["C10:L10"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.TachesMultiples.Should().ContainSingle().Which.Should().BeEquivalentTo(new TacheMultiplePivot(
            1, "Some action", "Acteur1", "RisqueX", "TM_PROC_MAD", new DateOnly(2026, 7, 16), estFactice: false,
            ligneSource: 9));
    }

    [Fact]
    public void Extract_WithBlankOrdre_CreatesFacticeTacheMultipleAlreadyValidated()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION TITLE";
        cells["C10:L10"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.TachesMultiples.Should().ContainSingle().Which.Should().BeEquivalentTo(new TacheMultiplePivot(
            null, "1-SECTION TITLE", "", "", "", null, estFactice: true, ligneSource: 9));
    }

    [Theory]
    [InlineData("MAD", "TM_PROC_MAD")]
    [InlineData("MAD ", "TM_PROC_MAD")]
    [InlineData("REL", "TM_PROC_REL")]
    [InlineData("REL ", "TM_PROC_REL")]
    [InlineData("mad", "TM_PROC_MAD")]
    public void Extract_MapsTypeTacheMultipleAlias(string alias, string expectedCode)
    {
        var cells = BaseHeaderCells();
        cells["B9"] = "1";
        cells["C9:L9"] = "Some action";
        cells["R9"] = alias;
        cells["C10:L10"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.TachesMultiples.Should().ContainSingle().Which.TypeTacheMultipleCode.Should().Be(expectedCode);
    }

    [Fact]
    public void Extract_WithUnrecognizedAlias_PassesThroughTrimmedRawValue()
    {
        var cells = BaseHeaderCells();
        cells["B9"] = "1";
        cells["C9:L9"] = "Some action";
        cells["R9"] = " XYZ ";
        cells["C10:L10"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.TachesMultiples.Should().ContainSingle().Which.TypeTacheMultipleCode.Should().Be("XYZ");
    }

    [Fact]
    public void Extract_StopsAtFirstBlankAction_WithoutReadingBeyond()
    {
        var cells = BaseHeaderCells();
        cells["B9"] = "1";
        cells["C9:L9"] = "Some action";
        cells["C10:L10"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "C11:L11"), Times.Never);
    }

    [Fact]
    public void Extract_WithMultipleTaskRows_ReadsAllUntilBlankAction()
    {
        var cells = BaseHeaderCells();
        cells["B9"] = "1";
        cells["C9:L9"] = "Action 1";
        cells["B10"] = null;
        cells["C10:L10"] = "2-SECTION";
        cells["B11"] = "2";
        cells["C11:L11"] = "Action 2";
        cells["C12:L12"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.TachesMultiples.Should().HaveCount(3);
        result.TachesMultiples[0].Ordre.Should().Be(1);
        result.TachesMultiples[1].EstFactice.Should().BeTrue();
        result.TachesMultiples[2].Ordre.Should().Be(2);

        // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
        // LigneSource must be the real source row, not the 0-based index in the returned list.
        result.TachesMultiples[0].LigneSource.Should().Be(9);
        result.TachesMultiples[1].LigneSource.Should().Be(10);
        result.TachesMultiples[2].LigneSource.Should().Be(11);
    }

    [Fact]
    public void Extract_WithBlankRepereHeader_RejectsWholeFileWithSingleBlockingError()
    {
        var cells = BaseHeaderCells();
        cells["M2:O2"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        result.Points.Should().BeEmpty();
        result.TachesMultiples.Should().BeEmpty();
        workbookReader.Verify(r => r.ReadCellValue(Sheet, "C9:L9"), Times.Never);
    }

    [Fact]
    public void Extract_WithRepereNotMatchingPrefix_RejectsWholeFileWithSingleBlockingError()
    {
        var cells = BaseHeaderCells();
        cells["M2:O2"] = "OTHER-38-C7401";
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnparsableValue);
    }

    [Fact]
    public void Extract_WithBlankDateRevision_RejectsWholeFileWithSingleBlockingError()
    {
        var cells = BaseHeaderCells(dateRevision: null!);
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnparsableValue);
    }

    [Fact]
    public void Extract_WithUnparsableDateRevision_RejectsWholeFileWithSingleBlockingError()
    {
        var cells = BaseHeaderCells(dateRevision: "not-a-date");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnparsableValue);
    }

    [Fact]
    public void Extract_WithDateRevisionIncludingTimeComponent_ParsesDatePartOnly()
    {
        var cells = BaseHeaderCells(dateRevision: "12/12/2025 00:00:00");
        cells["C9:L9"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Equipement!.Designation.Should().Be("Rév 2 du 12/12/2025");
    }

    // Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md), 32.3: synthetic
    // cases via Mock<IWorkbookReader> for the sandwich/bord/égalité-stricte wiring -- the C7401
    // sandwich case, D8570/G6306B non-regression, are covered against the real fixtures in
    // ProcedureExtractionServiceIntegrationTests (Infrastructure.Tests) instead.

    private static void AddTaskRow(Dictionary<string, string?> cells, int row, int ordre, string action, string alias)
    {
        cells[$"B{row}"] = ordre.ToString();
        cells[$"C{row}:L{row}"] = action;
        cells[$"R{row}"] = alias;
    }

    [Fact]
    public void Extract_WithMinorityTypeRunSurroundedByMajorityRuns_ProducesSandwichTypeIncoherenceWarning()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION";
        AddTaskRow(cells, 10, 1, "Action 1", "REL");
        AddTaskRow(cells, 11, 2, "Action 2", "MAD");
        AddTaskRow(cells, 12, 3, "Action 3", "REL");
        cells["C13:L13"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Errors.Should().ContainSingle();
        var error = result.Errors[0];
        error.Code.Should().Be(ExtractionErrorCode.TacheMultipleTypeMismatch);
        error.Sheet.Should().Be(Sheet);
        error.BlockIdentifier.Should().Be("1-SECTION (tâches 2-2)");
        error.Message.Should().Be(
            "Incohérence de TYPE détectée dans la tâche multiple \"1-SECTION\" : tâches 2–2 en TM_PROC_MAD, " +
            "encadrées par des tâches en TM_PROC_REL — vérifier une possible erreur de saisie.");
        result.TachesMultiples.Should().HaveCount(4);
        result.TachesMultiples.Should().Contain(t => t.Ordre == 2 && t.TypeTacheMultipleCode == "TM_PROC_MAD");
    }

    [Fact]
    public void Extract_WithMinorityTypeRunAtTheStartOfSection_ProducesDebutDeSectionTypeIncoherenceWarning()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION";
        AddTaskRow(cells, 10, 1, "Action 1", "MAD");
        AddTaskRow(cells, 11, 2, "Action 2", "REL");
        AddTaskRow(cells, 12, 3, "Action 3", "REL");
        cells["C13:L13"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Errors.Should().ContainSingle();
        var error = result.Errors[0];
        error.Code.Should().Be(ExtractionErrorCode.TacheMultipleTypeMismatch);
        error.BlockIdentifier.Should().Be("1-SECTION (tâches 1-1)");
        error.Message.Should().Be(
            "Incohérence de TYPE détectée dans la tâche multiple \"1-SECTION\" : tâches 1–1 en TM_PROC_MAD, " +
            "en début de section, adjacentes à des tâches en TM_PROC_REL — vérifier une possible erreur de saisie.");
    }

    [Fact]
    public void Extract_WithMinorityTypeRunAtTheEndOfSection_ProducesFinDeSectionTypeIncoherenceWarning()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION";
        AddTaskRow(cells, 10, 1, "Action 1", "REL");
        AddTaskRow(cells, 11, 2, "Action 2", "REL");
        AddTaskRow(cells, 12, 3, "Action 3", "MAD");
        cells["C13:L13"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Errors.Should().ContainSingle();
        var error = result.Errors[0];
        error.Code.Should().Be(ExtractionErrorCode.TacheMultipleTypeMismatch);
        error.BlockIdentifier.Should().Be("1-SECTION (tâches 3-3)");
        error.Message.Should().Be(
            "Incohérence de TYPE détectée dans la tâche multiple \"1-SECTION\" : tâches 3–3 en TM_PROC_MAD, " +
            "en fin de section, adjacentes à des tâches en TM_PROC_REL — vérifier une possible erreur de saisie.");
    }

    [Fact]
    public void Extract_WithStrictlyEqualTypeSplit_ProducesSingleAmbiguousSectionWarning_NotOnePerType()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION";
        AddTaskRow(cells, 10, 1, "Action 1", "REL");
        AddTaskRow(cells, 11, 2, "Action 2", "MAD");
        cells["C12:L12"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Errors.Should().ContainSingle();
        var error = result.Errors[0];
        error.Code.Should().Be(ExtractionErrorCode.TacheMultipleTypeMismatch);
        error.BlockIdentifier.Should().Be("1-SECTION");
        error.Message.Should().Be(
            "Répartition de TYPE ambiguë dans la tâche multiple \"1-SECTION\" : TM_PROC_REL (1–1) et TM_PROC_MAD (2–2) " +
            "se partagent la section à parts égales — impossible de déterminer le type correct, vérifier manuellement.");
        result.TachesMultiples.Should().HaveCount(3);
        result.TachesMultiples.Should().Contain(t => t.Ordre == 1 && t.TypeTacheMultipleCode == "TM_PROC_REL");
        result.TachesMultiples.Should().Contain(t => t.Ordre == 2 && t.TypeTacheMultipleCode == "TM_PROC_MAD");
    }

    [Fact]
    public void Extract_WithHomogeneousSection_ProducesNoTypeIncoherenceWarning()
    {
        var cells = BaseHeaderCells();
        cells["C9:L9"] = "1-SECTION";
        AddTaskRow(cells, 10, 1, "Action 1", "REL");
        AddTaskRow(cells, 11, 2, "Action 2", "REL");
        cells["C12:L12"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);

        result.Errors.Should().BeEmpty();
    }
}
