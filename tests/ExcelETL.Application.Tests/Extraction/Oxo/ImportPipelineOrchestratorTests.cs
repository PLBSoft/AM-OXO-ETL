using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class ImportPipelineOrchestratorTests
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";

    private readonly Mock<IProcedureExtractionService> _procedureService = new();
    private readonly Mock<IElementSheetExtractionService> _elementService = new();

    private readonly ImportPipelineOrchestrator _sut;

    public ImportPipelineOrchestratorTests()
    {
        _sut = new ImportPipelineOrchestrator(
            _procedureService.Object, _elementService.Object, NullLogger<ImportPipelineOrchestrator>.Instance);
    }

    // Lot 084.6: one element service for the five element sheets, set up per sheet name.
    private void SetupSheet(
        string sheet, IReadOnlyList<IsolementPivot>? elements = null, IReadOnlyList<PointPivot>? points = null,
        IReadOnlyList<ExtractionError>? errors = null, string zone = "") =>
        _elementService
            .Setup(s => s.Extract(It.IsAny<IWorkbookReader>(), It.Is<SheetExtractionRule>(r => r.SheetName == sheet), It.IsAny<string>()))
            .Returns(new ElementSheetExtractionResult(elements ?? [], points ?? [], errors ?? [], zone));

    private void SetupEmptyElementSheets(string diversZone = "")
    {
        foreach (var sheet in new[] { "ISOLEMENT", "PLATINES", "ORIFICES CAPACITES", "AUTRES JOINTS TOUCHES" })
        {
            SetupSheet(sheet);
        }

        SetupSheet("DIVERS", zone: diversZone);
    }

    private static RepeatingBlockLocator TrivialLocator(string sheet) =>
        new(sheet, 1, 1, [new BlockFieldDefinition("Stop", "A", 0, 0)]);

    private static ImportProfile CreateProfile(
        IReadOnlyList<string>? defaultTableaux = null, IReadOnlyList<string>? defaultApplicationNames = null,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null) => new(
        "Profil OXO standard", ReperePrefix, EquipementTypeElementNom,
        defaultTableaux ?? [], defaultApplicationNames ?? [],
        [
            new SheetExtractionRule("PROCEDURE", TrivialLocator("PROCEDURE"), [], [], [], []),
            new SheetExtractionRule("ISOLEMENT", TrivialLocator("ISOLEMENT"), [], [], [], []),
            new SheetExtractionRule("PLATINES", TrivialLocator("PLATINES"), [], [], [], []),
            new SheetExtractionRule("ORIFICES CAPACITES", TrivialLocator("ORIFICES CAPACITES"), [], [], [], []),
            new SheetExtractionRule("AUTRES JOINTS TOUCHES", TrivialLocator("AUTRES JOINTS TOUCHES"), [], [], [], []),
            new SheetExtractionRule("DIVERS", TrivialLocator("DIVERS"), [], [], [], [])
        ],
        tacheMultipleTypeLabels);

    private static ImportResult RejectedProcedureResult() => new(
        null, [], [], [],
        [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "Cellule M2:O2 introuvable ou vide.")]);

    private static ImportResult ValidProcedureResult() => new(
        new EquipementPivot("38-C7401", "Rév 2 du 12/12/2025", EquipementTypeElementNom),
        [],
        [new PointPivot("VISITE PRÉALABLE CHANTIER", "38-C7401")],
        [new TacheMultiplePivot(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", null, false, 52)],
        []);

    [Fact]
    public void Run_WhenProcedureFails_ReturnsImmediatelyWithoutCallingTheOtherFiveServices()
    {
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(RejectedProcedureResult());
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.TachesMultiples.Should().BeEmpty();

        _elementService.Verify(
            s => s.Extract(It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Run_WhenProcedureSucceeds_AggregatesEverythingFromAllSixSources()
    {
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(ValidProcedureResult());
        SetupSheet("ISOLEMENT",
            [new IsolementPivot("38-C7401-V1", "Vanne 1", "PROLOCK", "FERMÉE", "")],
            [new PointPivot("PROLOCK VANNES", "38-C7401-V1")]);
        SetupSheet("PLATINES",
            [new IsolementPivot("38-C7401-PT1", "Platine 1", "PLATINE", "", "")],
            [new PointPivot("POSE ÉTIQUETTES", "38-C7401-PT1")]);
        SetupSheet("ORIFICES CAPACITES",
            [new IsolementPivot("38-C7401-TH1", "Trou 1", "TROU D'HOMME", "", "")],
            [new PointPivot("POSE ÉTIQUETTES", "38-C7401-TH1")]);
        SetupSheet("AUTRES JOINTS TOUCHES",
            [new IsolementPivot("38-C7401-J1", "Joint 1", "TUYAUTERIE", "", "")],
            [new PointPivot("CONTRÔLE ETANCHÉITÉS", "38-C7401-J1")]);
        SetupSheet("DIVERS",
            [new IsolementPivot("38-C7401-LT1", "Transmetteur", "INSTRUMENTATION", "", "")],
            [new PointPivot("SYNCHRONISATION INSTRUMENTATION", "38-C7401-LT1")],
            [new ExtractionError("DIVERS", "38-C7401-XX", ExtractionErrorCode.NoConditionalPointCreated, "warning")],
            "ZONE 1");
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.Equipement.Should().NotBeNull();
        result.Isolements.Should().HaveCount(5);
        result.Isolements.Select(i => i.Repere).Should().BeEquivalentTo(
        [
            "38-C7401-V1", "38-C7401-PT1", "38-C7401-TH1", "38-C7401-J1", "38-C7401-LT1"
        ]);
        // 2 from PROCEDURE + 1 each from the 5 other sheets.
        result.Points.Should().HaveCount(6);
        result.TachesMultiples.Should().ContainSingle();
        result.Errors.Should().ContainSingle();

        _elementService.Verify(
            s => s.Extract(It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), It.IsAny<string>()), Times.Exactly(5));
    }

    [Fact]
    public void Run_BroadcastsLoc1FromDiversOntoEquipementAndEveryIsolement()
    {
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(ValidProcedureResult());
        SetupEmptyElementSheets("ZONE 4");
        SetupSheet("ISOLEMENT", [new IsolementPivot("38-C7401-V1", "Vanne 1", "PROLOCK", "FERMÉE", "")]);
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.Equipement!.Localisation.Should().Be("ZONE 4");
        result.Isolements.Should().ContainSingle().Which.Localisation.Should().Be("ZONE 4");
    }

    [Fact]
    public void Run_BroadcastsDefaultTableauxApplicationsAndRepereParentFromProfileOntoEquipementAndEveryIsolement()
    {
        var defaultTableaux = new[] { "TRAVAUX COMPLET", "TRAVAUX DETAIL" };
        var defaultApplicationNames = new[] { "PROGRESS" };
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(ValidProcedureResult());
        SetupEmptyElementSheets();
        SetupSheet("ISOLEMENT", [new IsolementPivot("38-C7401-V1", "Vanne 1", "PROLOCK", "FERMÉE", "")]);
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(
            workbookReader, CreateProfile(defaultTableaux: defaultTableaux, defaultApplicationNames: defaultApplicationNames));

        result.Equipement!.Tableaux.Should().BeEquivalentTo(defaultTableaux, o => o.WithStrictOrdering());
        result.Equipement.Applications.Should().BeEquivalentTo(defaultApplicationNames);
        result.Isolements.Should().ContainSingle();
        var isolement = result.Isolements.Single();
        isolement.Tableaux.Should().BeEquivalentTo(defaultTableaux, o => o.WithStrictOrdering());
        isolement.Applications.Should().BeEquivalentTo(defaultApplicationNames);
        isolement.RepereParent.Should().Be(result.Equipement.Repere);
    }

    [Fact]
    public void Run_WithEmptyDefaultTableauxAndApplicationNames_ProducesEmptyListsOnAllPivots()
    {
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(ValidProcedureResult());
        SetupEmptyElementSheets();
        SetupSheet("ISOLEMENT", [new IsolementPivot("38-C7401-V1", "Vanne 1", "PROLOCK", "FERMÉE", "")]);
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.Equipement!.Tableaux.Should().BeEmpty();
        result.Equipement.Applications.Should().BeEmpty();
        result.Isolements.Should().ContainSingle();
        var isolement = result.Isolements.Single();
        isolement.Tableaux.Should().BeEmpty();
        isolement.Applications.Should().BeEmpty();
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md).
    private void SetupMinimalSuccessfulRun(IReadOnlyList<TacheMultiplePivot>? tachesMultiples = null)
    {
        var procedureResult = tachesMultiples is null
            ? ValidProcedureResult()
            : new ImportResult(
                new EquipementPivot("38-C7401", "Rév 2 du 12/12/2025", EquipementTypeElementNom), [], [], tachesMultiples, []);

        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(procedureResult);
        SetupEmptyElementSheets();
    }

    [Fact]
    public void Run_BroadcastsEquipementRepereAndTypeElementNomOntoEveryTacheMultiple()
    {
        SetupMinimalSuccessfulRun([
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false, 52),
            new TacheMultiplePivot(2, "Déconsigner", "ADF", "Aucun", "TM_PROC_REL", null, false, 53)
        ]);
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.TachesMultiples.Should().HaveCount(2);
        result.TachesMultiples.Should().OnlyContain(t => t.Repere == result.Equipement!.Repere);
        result.TachesMultiples.Should().OnlyContain(t => t.TypeElementNom == EquipementTypeElementNom);
    }

    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
    // Localisation follows the exact same broadcast mechanism as Repere/TypeElementNom above, from the
    // Equipement's own already-diffused Localisation (loc1), not from DIVERS directly.
    [Fact]
    public void Run_BroadcastsEquipementLocalisationOntoEveryTacheMultiple()
    {
        _procedureService
            .Setup(s => s.Extract(
                It.IsAny<IWorkbookReader>(), It.IsAny<SheetExtractionRule>(), ReperePrefix, EquipementTypeElementNom))
            .Returns(new ImportResult(
                new EquipementPivot("38-C7401", "Rév 2 du 12/12/2025", EquipementTypeElementNom), [], [],
                [
                    new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false, 52),
                    new TacheMultiplePivot(2, "Déconsigner", "ADF", "Aucun", "TM_PROC_REL", null, false, 53)
                ],
                []));
        SetupEmptyElementSheets("ZONE 4");
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.TachesMultiples.Should().HaveCount(2);
        result.TachesMultiples.Should().OnlyContain(t => t.Localisation == "ZONE 4");
    }

    [Fact]
    public void Run_ResolvesColonneTravaux_FromMatchingTacheMultipleTypeLabel_TrimmedAndCaseInsensitive()
    {
        SetupMinimalSuccessfulRun([
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false, 52),
            new TacheMultiplePivot(2, "Déconsigner", "ADF", "Aucun", " tm_proc_rel ", null, false, 53)
        ]);
        var workbookReader = Mock.Of<IWorkbookReader>();
        var profile = CreateProfile(tacheMultipleTypeLabels:
        [
            new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"),
            new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure REL")
        ]);

        var result = _sut.Run(workbookReader, profile);

        result.TachesMultiples.Should().Contain(t => t.TypeTacheMultipleCode == "TM_PROC_MAD" && t.ColonneTravaux == "Procédure MAD");
        result.TachesMultiples.Should().Contain(t => t.ColonneTravaux == "Procédure REL");
    }

    [Fact]
    public void Run_WhenNoTacheMultipleTypeLabelMatches_LeavesColonneTravauxBlank()
    {
        SetupMinimalSuccessfulRun([new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false, 52)]);
        var workbookReader = Mock.Of<IWorkbookReader>();
        var profile = CreateProfile(tacheMultipleTypeLabels: [new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure REL")]);

        var result = _sut.Run(workbookReader, profile);

        result.TachesMultiples.Should().ContainSingle().Which.ColonneTravaux.Should().BeEmpty();
    }

    [Fact]
    public void Run_WithEmptyTacheMultipleTypeLabels_LeavesColonneTravauxBlankForEveryTache()
    {
        SetupMinimalSuccessfulRun([new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false, 52)]);
        var workbookReader = Mock.Of<IWorkbookReader>();

        var result = _sut.Run(workbookReader, CreateProfile());

        result.TachesMultiples.Should().ContainSingle().Which.ColonneTravaux.Should().BeEmpty();
    }
}
