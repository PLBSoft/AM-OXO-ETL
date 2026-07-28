using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Lot D2: runs the full pipeline (Lot D1's ImportPipelineOrchestrator, wired with the real Lot C1-C6
// services) against all 3 real client fixtures via the real ClosedXmlWorkbookReader. Also serves as
// the regression guard-rail the ticket calls for -- one test per file, hardcoded profile assembled
// from the same cell ranges already validated individually by each sheet's own integration tests.
public class ImportPipelineOrchestratorIntegrationTests
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";
    private static readonly string[] DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"];
    private static readonly string[] DefaultApplicationNames = ["PROGRESS"];

    private readonly ImportPipelineOrchestrator _sut = new(
        new ProcedureExtractionService(new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ProcedureExtractionService>.Instance),
        new IsolementExtractionService(
            new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(), NullLogger<IsolementExtractionService>.Instance),
        new UnconditionalIsolementSheetExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(),
            NullLogger<UnconditionalIsolementSheetExtractionService>.Instance),
        new AutresJointsTouchesExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<AutresJointsTouchesExtractionService>.Instance),
        new DiversExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<DiversExtractionService>.Instance),
        NullLogger<ImportPipelineOrchestrator>.Instance);

    private static ImportProfile CreateProfile() => new(
        "Profil OXO standard", ReperePrefix, EquipementTypeElementNom,
        DefaultTableaux, DefaultApplicationNames,
        [
            new SheetExtractionRule(
                "PROCEDURE",
                new RepeatingBlockLocator("PROCEDURE", 9, 1, ProcedureFieldNames.Action,
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
                    new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell("PROCEDURE", "P2:Q2")),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy")
                ],
                [
                    new HeaderCompositeRule(
                        ProcedureHeaderFieldNames.Designation,
                        $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
                ]),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator("PLATINES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    "POSE ÉTIQUETTES",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS",
                    "RECEPTION DEBUT MAD",
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RECEPTION DEBUT REL",
                    "PLATINES / TAMPONS PLEINS"
                ], [], []),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator("ORIFICES CAPACITES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ], [], []),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("AUTRES JOINTS TOUCHES", "N6"))],
                []),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator("DIVERS", 9, 3, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("DIVERS", "N6"))],
                [])
        ]);

    [Fact]
    public void Run_C7401Fixture_ProducesExpectedEquipementAndBroadcastsLoc1Everywhere()
    {
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("38-C7401");
        result.Equipement.Designation.Should().Be("Rév 2 du 12/12/2025");
        result.Equipement.TypeElementNom.Should().Be(EquipementTypeElementNom);
        result.Equipement.Localisation.Should().Be("ZONE 1");
        result.Equipement.Tableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        result.Equipement.Applications.Should().Equal("PROGRESS");

        // ISOLEMENT(8) + PLATINES(15) + ORIFICES CAPACITES(0) + AUTRES JOINTS TOUCHES(0) + DIVERS(0)
        result.Isolements.Should().HaveCount(23);
        result.Isolements.Should().OnlyContain(i => i.Localisation == "ZONE 1");
        result.Isolements.Should().OnlyContain(i => i.RepereParent == "38-C7401");
        result.Isolements.Should().OnlyContain(i => i.Tableaux.SequenceEqual(DefaultTableaux));
        result.Isolements.Should().OnlyContain(i => i.Applications.SequenceEqual(DefaultApplicationNames));
        result.TachesMultiples.Should().HaveCount(98);
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void Run_D8570Fixture_ExtractsVanneIsolementAlongsideEverythingElse()
    {
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("644-D8570");
        result.Equipement.Designation.Should().Be("Rév 0 du 11/09/2025");
        result.Equipement.Localisation.Should().Be("ZONE 4");
        result.Equipement.Tableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        result.Equipement.Applications.Should().Equal("PROGRESS");

        // ISOLEMENT(15, incl. VANNE) + PLATINES(21) + ORIFICES CAPACITES(5) + AUTRES JOINTS TOUCHES(13) + DIVERS(13)
        result.Isolements.Should().HaveCount(67);
        result.Isolements.Should().OnlyContain(i => i.Localisation == "ZONE 4");
        result.Isolements.Should().OnlyContain(i => i.RepereParent == "644-D8570");
        result.TachesMultiples.Should().NotBeEmpty();
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);

        var vanne = result.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE").Which;
        result.Errors.Should().Contain(e =>
            e.Code == ExtractionErrorCode.UnrecognizedTypeElement && e.BlockIdentifier == vanne.Repere);
    }

    [Fact]
    public void Run_G6306BFixture_ProducesExpectedEquipementAndBroadcastsLoc1Everywhere()
    {
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("602-G6306B");
        result.Equipement.Designation.Should().Be("Rév 0 du 12/05/2025");
        result.Equipement.Localisation.Should().Be("ZONE 4");
        result.Equipement.Tableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        result.Equipement.Applications.Should().Equal("PROGRESS");

        // ISOLEMENT(3) + PLATINES(5) + ORIFICES CAPACITES(2) + AUTRES JOINTS TOUCHES(4) + DIVERS(4)
        result.Isolements.Should().HaveCount(18);
        result.Isolements.Should().OnlyContain(i => i.Localisation == "ZONE 4");
        result.Isolements.Should().OnlyContain(i => i.RepereParent == "602-G6306B");
        result.TachesMultiples.Should().NotBeEmpty();
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
    }

    private ImportResult RunOnFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Run(workbookReader, CreateProfile());
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
