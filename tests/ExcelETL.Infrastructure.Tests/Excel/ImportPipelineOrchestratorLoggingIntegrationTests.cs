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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Lot G2: proves the ILogger instrumentation added in Lot G1/G2 actually surfaces the fields the
// ticket asked for (duration, sheet count, total extracted-element count, and the D8570 "VANNE"
// non-blocking warning) on a real pipeline run against the real fixtures -- not just that the code
// compiles against a Mock<ILogger>. Reuses the same hardcoded profile/fixture-path helper as
// ImportPipelineOrchestratorIntegrationTests (duplicated rather than shared, matching this repo's
// established no-shared-test-helper convention, see CLAUDE.md).
public class ImportPipelineOrchestratorLoggingIntegrationTests
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    private readonly CapturedLogEntries _orchestratorLog = new();
    private readonly CapturedLogEntries _isolementLog = new();
    private readonly ImportPipelineOrchestrator _sut;

    public ImportPipelineOrchestratorLoggingIntegrationTests()
    {
        _sut = new ImportPipelineOrchestrator(
            new ProcedureExtractionService(new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ProcedureExtractionService>.Instance),
            new IsolementExtractionService(
                new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
                new CapturingLogger<IsolementExtractionService>(_isolementLog)),
            new UnconditionalIsolementSheetExtractionService(
                new RepeatingBlockReader(), new TextTransformEvaluator(),
                NullLogger<UnconditionalIsolementSheetExtractionService>.Instance),
            new AutresJointsTouchesExtractionService(
                new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
                new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<AutresJointsTouchesExtractionService>.Instance),
            new DiversExtractionService(
                new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
                new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<DiversExtractionService>.Instance),
            new CapturingLogger<ImportPipelineOrchestrator>(_orchestratorLog));
    }

    [Fact]
    public void Run_G6306BFixture_LogsDurationSheetCountAndTotalElementCountOnCompletion()
    {
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        var totalElementCount = result.Isolements.Count + result.Points.Count + result.TachesMultiples.Count;

        var completion = _orchestratorLog.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Information && e.Message.StartsWith("Completed import pipeline run")).Which;

        completion.Message.Should().MatchRegex(@"in \d+ms");
        completion.Message.Should().Contain("6 sheet(s) processed");
        completion.Message.Should().Contain($"{totalElementCount} element(s) extracted");
    }

    [Fact]
    public void Run_D8570Fixture_LogsVanneWarning_NotOnlyInImportResultErrors()
    {
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        var vanne = result.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE").Which;
        result.Errors.Should().Contain(e =>
            e.Code == ExtractionErrorCode.NoConditionalPointCreated && e.BlockIdentifier == vanne.Repere);

        _isolementLog.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(nameof(ExtractionErrorCode.NoConditionalPointCreated)) &&
            e.Message.Contains(vanne.Repere));
    }

    private ImportResult RunOnFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Run(workbookReader, CreateProfile());
    }

    private static ImportProfile CreateProfile() => new(
        "Profil OXO standard", ReperePrefix, EquipementTypeElementNom,
        ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], ["PROGRESS"],
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
