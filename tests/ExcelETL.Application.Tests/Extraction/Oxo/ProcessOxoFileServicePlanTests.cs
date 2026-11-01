using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Plans;
using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.6, D11/D13): plan sheets printed to
// PDF while processing an accepted file, archived next to the generated workbook, best-effort.
public class ProcessOxoFileServicePlanTests
{
    private const string Paysage = "PLANS ISOLEMENT (Paysage)";
    private const string Platinage = "PLANS PLATINAGE & JOINTS";

    private static readonly byte[] SourceContent = [1, 2, 3, 4];

    private readonly Mock<IImportProfileStore> _importProfileStore = new();
    private readonly Mock<IExportProfileStore> _exportProfileStore = new();
    private readonly Mock<IImportPipelineOrchestrator> _orchestrator = new();
    private readonly Mock<ISheetGenerationEngine> _generationEngine = new();
    private readonly Mock<IGeneratedFileWriter> _fileWriter = new();
    private readonly Mock<IGeneratedFileArchiveStore> _archiveStore = new();
    private readonly Mock<IWorkbookReaderFactory> _readerFactory = new();
    private readonly Mock<IPlanSheetPdfRenderer> _renderer = new();
    private readonly List<string> _planFileNames = [];
    private GeneratedFileRecord? _saved;
    private readonly ProcessOxoFileService _sut;

    public ProcessOxoFileServicePlanTests()
    {
        _fileWriter
            .Setup(w => w.WriteSourceAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"2026\10\source.xlsx");
        _fileWriter
            .Setup(w => w.WriteTargetAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"2026\10\target.xlsx");
        _fileWriter
            .Setup(w => w.WritePlanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, DateTime, CancellationToken>((_, name, _, _) => _planFileNames.Add(name))
            .ReturnsAsync((Stream _, string name, DateTime _, CancellationToken _) => @"2026\10\plan_" + name);
        _archiveStore
            .Setup(s => s.SaveAsync(It.IsAny<GeneratedFileRecord>(), It.IsAny<CancellationToken>()))
            .Callback<GeneratedFileRecord, CancellationToken>((r, _) => _saved = r);

        _sut = new ProcessOxoFileService(
            _importProfileStore.Object, _exportProfileStore.Object, _orchestrator.Object, _generationEngine.Object,
            Mock.Of<IWorkbookWriter>(), _fileWriter.Object, _archiveStore.Object, _readerFactory.Object,
            Mock.Of<IGeneratedFilePurger>(), _renderer.Object, NullLogger<ProcessOxoFileService>.Instance);
    }

    private ProcessOxoFileCommand Arrange(ImportResult importResult, IReadOnlyList<string> planSheetNames)
    {
        var importProfile = new ImportProfile(
            "Profil import", "MAD", [], [],
            [new SheetExtractionRule("PROCEDURE", new RepeatingBlockLocator(1, 1, [new BlockFieldDefinition("Action", "A", 0, 0)]), [], [], [], [])],
            [], planSheetNames);
        var exportProfile = new ExportProfile(
            "Profil export",
            [new SheetGenerationRule("Parents", PivotSource.Equipement, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)], [], [])]);
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var reader = Mock.Of<IWorkbookReader>();
        _readerFactory.Setup(f => f.Open(SourceContent)).Returns(reader);
        _orchestrator.Setup(o => o.Run(reader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        return new ProcessOxoFileCommand(importProfileId, exportProfileId, "source.xlsx", SourceContent);
    }

    private static ImportResult Accepted() => new(new EquipementPivot("38-C7401", "Compresseur", "MAD"), [], [], [], []);

    private static ImportResult Rejected() => new(
        null, [], [], [], [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "vide")]);

    private void RenderSheet(string requested, string actualName, params byte[][] pages) =>
        _renderer.Setup(r => r.RenderAsync(SourceContent, requested, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedPlanSheet(actualName, pages));

    [Fact]
    public async Task AcceptedFile_EachPlanPage_IsArchivedAsItsOwnPdf_NamedAfterTheGeneratedWorkbook()
    {
        var command = Arrange(Accepted(), [Paysage, Platinage]);
        RenderSheet(Paysage, "PLANS ISOLEMENT (Paysage) ", [10], [20, 21]);
        RenderSheet(Platinage, Platinage, [30, 31, 32]);

        var result = await _sut.ProcessAsync(command);

        var baseName = Path.GetFileNameWithoutExtension(result.GeneratedFileName);
        _saved!.Plans.Should().Equal(
            new GeneratedFilePlan(Paysage, 1, $"{baseName}_{Paysage}_p1.pdf", $@"2026\10\plan_{baseName}_{Paysage}_p1.pdf", 1),
            new GeneratedFilePlan(Paysage, 2, $"{baseName}_{Paysage}_p2.pdf", $@"2026\10\plan_{baseName}_{Paysage}_p2.pdf", 2),
            new GeneratedFilePlan(Platinage, 1, $"{baseName}_{Platinage}_p1.pdf", $@"2026\10\plan_{baseName}_{Platinage}_p1.pdf", 3));
        _planFileNames.Should().HaveCount(3);
    }

    [Fact]
    public async Task SheetMissingOrWithoutPlan_ArchivesNothingForIt()
    {
        var command = Arrange(Accepted(), [Paysage, Platinage]);
        _renderer.Setup(r => r.RenderAsync(SourceContent, Paysage, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RenderedPlanSheet?)null);
        RenderSheet(Platinage, Platinage);

        await _sut.ProcessAsync(command);

        _saved!.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectedFile_PrintsNoPlan()
    {
        var command = Arrange(Rejected(), [Paysage]);

        var result = await _sut.ProcessAsync(command);

        result.ImportResult.Equipement.Should().BeNull();
        _renderer.Verify(r => r.RenderAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _saved!.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task NoPlanSheetInTheProfile_NeverCallsTheRenderer()
    {
        var command = Arrange(Accepted(), []);

        await _sut.ProcessAsync(command);

        _renderer.Verify(r => r.RenderAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _saved!.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderingFailure_SkipsThatSheet_AndStillReturnsTheGeneratedWorkbook()
    {
        var command = Arrange(Accepted(), [Paysage, Platinage]);
        _renderer.Setup(r => r.RenderAsync(SourceContent, Paysage, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LibreOffice absent"));
        RenderSheet(Platinage, Platinage, [1]);

        var result = await _sut.ProcessAsync(command);

        result.GeneratedFileStream.Should().NotBeNull();
        result.ArchivedRecordId.Should().NotBeNull();
        _saved!.Plans.Should().ContainSingle().Which.SheetName.Should().Be(Platinage);
    }
}
