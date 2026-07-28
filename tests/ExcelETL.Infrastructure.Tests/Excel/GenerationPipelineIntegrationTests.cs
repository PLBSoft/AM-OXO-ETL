using ClosedXML.Excel;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Lot I5: import -> pivot -> generation, end to end, against all 3 real client fixtures. Duplicates
// ImportPipelineOrchestratorIntegrationTests' hardcoded ImportProfile/fixture-path helper rather than
// sharing it, matching this repo's established no-shared-test-helper convention (see
// ImportPipelineOrchestratorLoggingIntegrationTests). The ExportProfile below is a deliberate, partial
// approximation of OXO_TRAME_IMPORT_MAD.xlsx's 2-sheet (Parents/Enfants) shape -- not full column
// fidelity, per this lot's ticket ("approximation de travail").
public class GenerationPipelineIntegrationTests
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    private readonly ImportPipelineOrchestrator _orchestrator = new(
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

    private readonly SheetGenerationEngine _engine = new(NullLogger<SheetGenerationEngine>.Instance);
    private readonly ClosedXmlWorkbookWriter _writer = new(NullLogger<ClosedXmlWorkbookWriter>.Instance);

    private static ImportProfile CreateImportProfile() => new(
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

    // Approximates OXO_TRAME_IMPORT_MAD.xlsx: 2 sheets (Parents/Enfants), a mix of mapped descriptive
    // columns and unmapped ones (Source = null, per the spec doc's own "colonnes non encore mappées"
    // list), a handful of representative Point columns (not the full Colonne.Nom catalogue), and no
    // Tâches Multiples sheet at all (explicitly out of scope for this lot).
    private static ExportProfile CreateExportProfile() => new(
        "Profil export OXO standard",
        [
            new SheetGenerationRule(
                "Parents",
                PivotSource.Equipement,
                [
                    new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
                    new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation),
                    new ColumnDefinition("Type Elément", PivotFieldRef.EquipementTypeElementNom),
                    new ColumnDefinition("Zone", PivotFieldRef.EquipementLocalisation),
                    new ColumnDefinition("Fluide", null),
                    new ColumnDefinition("Commentaires", null)
                ],
                [
                    new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet"),
                    new PointColumnDefinition("TRAVAUX DETAIL", "Travaux détail")
                ],
                []),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    new ColumnDefinition("Type", PivotFieldRef.IsolementTypeElementNom),
                    new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation),
                    new ColumnDefinition("Position à la pose", PivotFieldRef.IsolementPositionALaPose),
                    new ColumnDefinition("Zone", PivotFieldRef.IsolementLocalisation),
                    new ColumnDefinition("Phase process", null),
                    new ColumnDefinition("Remarques", null)
                ],
                [
                    new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
                    new PointColumnDefinition("DEPROLOCK VANNES", "Deprolock vannes"),
                    new PointColumnDefinition(ZeroEnergieColonneName, "Zéro énergie en présence EE (PS941)")
                ],
                [])
        ]);

    [Fact]
    public void GenerateFromC7401Fixture_ProducesExpectedStructureAndValues()
    {
        var (importResult, generated) = RunPipeline("Dossier.de.MaD.IDL.-.C7401.xlsx");

        generated.Worksheets.Select(ws => ws.Name).Should().Equal("Parents", "Enfants");

        var parents = generated.Worksheet("Parents");
        parents.Cell(1, 1).GetString().Should().Be("Repère");
        parents.Cell(2, 1).GetString().Should().Be("38-C7401");
        parents.Cell(2, 4).GetString().Should().Be("ZONE 1");

        var enfants = generated.Worksheet("Enfants");
        enfants.Cell(1, 1).GetString().Should().Be("Numéro");
        var lastDataRow = 1 + importResult.Isolements.Count;
        enfants.RowsUsed().Should().HaveCount(1 + importResult.Isolements.Count);
        enfants.Cell(lastDataRow, 5).GetString().Should().Be("ZONE 1");
    }

    [Fact]
    public void GenerateFromD8570Fixture_KeepsUnrecognizedTypeElementIsolementAsNormalRow()
    {
        var (importResult, generated) = RunPipeline("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        var vanne = importResult.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE").Which;

        // Repere alone isn't a safe lookup key here: it's composed independently per source sheet
        // (K6:T6-Identification), so a different sheet's row can coincidentally share the same
        // Identification and land on the same composed Repere text -- disambiguate on Type too.
        var enfants = generated.Worksheet("Enfants");
        var vanneRow = enfants.RowsUsed()
            .Should().ContainSingle(row => row.Cell(1).GetString() == vanne.Repere && row.Cell(2).GetString() == "VANNE").Which;
        vanneRow.Cell(5).GetString().Should().Be("ZONE 4");
    }

    [Fact]
    public void GenerateFromG6306BFixture_ProducesExpectedStructureAndValues()
    {
        var (importResult, generated) = RunPipeline("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        var parents = generated.Worksheet("Parents");
        parents.Cell(2, 1).GetString().Should().Be("602-G6306B");
        parents.Cell(2, 4).GetString().Should().Be("ZONE 4");

        var enfants = generated.Worksheet("Enfants");
        enfants.RowsUsed().Should().HaveCount(1 + importResult.Isolements.Count);
    }

    private (ImportResult ImportResult, XLWorkbook Generated) RunPipeline(string fixtureFileName)
    {
        ImportResult importResult;
        using (var sourceStream = File.OpenRead(FixturePath(fixtureFileName)))
        using (var workbookReader = new ClosedXmlWorkbookReader(sourceStream))
        {
            importResult = _orchestrator.Run(workbookReader, CreateImportProfile());
        }

        var generatedWorkbook = _engine.Generate(importResult, CreateExportProfile());

        using var destination = new MemoryStream();
        _writer.Write(generatedWorkbook, destination);

        return (importResult, new XLWorkbook(destination));
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
