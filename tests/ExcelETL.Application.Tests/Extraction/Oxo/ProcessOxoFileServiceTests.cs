using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Generation;
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

public class ProcessOxoFileServiceTests
{
    private static readonly byte[] SampleSourceContent = [1, 2, 3, 4];

    private readonly Mock<IImportProfileStore> _importProfileStore = new();
    private readonly Mock<IExportProfileStore> _exportProfileStore = new();
    private readonly Mock<IImportPipelineOrchestrator> _orchestrator = new();
    private readonly Mock<ISheetGenerationEngine> _generationEngine = new();
    private readonly Mock<IWorkbookWriter> _workbookWriter = new();
    private readonly Mock<IGeneratedFileWriter> _generatedFileWriter = new();
    private readonly Mock<IGeneratedFileArchiveStore> _generatedFileArchiveStore = new();
    private readonly ProcessOxoFileService _sut;

    public ProcessOxoFileServiceTests()
    {
        _generatedFileWriter
            .Setup(w => w.WriteSourceAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"2026\07\source.xlsx");
        _generatedFileWriter
            .Setup(w => w.WriteTargetAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"2026\07\target.xlsx");

        _sut = new ProcessOxoFileService(
            _importProfileStore.Object,
            _exportProfileStore.Object,
            _orchestrator.Object,
            _generationEngine.Object,
            _workbookWriter.Object,
            _generatedFileWriter.Object,
            _generatedFileArchiveStore.Object,
            NullLogger<ProcessOxoFileService>.Instance);
    }

    private static ImportProfile CreateImportProfile() => new(
        "Profil import test", "MAD TRAVAUX", [], [],
        [new SheetExtractionRule(
            "PROCEDURE",
            new RepeatingBlockLocator("PROCEDURE", 1, 1, "Stop", [new BlockFieldDefinition("Stop", "A", 0, 0)]),
            [],
            [], [], [])]);

    private static ExportProfile CreateExportProfile() => new(
        "Profil export test",
        [new SheetGenerationRule(
            "Parents", PivotSource.Equipement, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)], [], [])]);

    private static ImportResult AcceptedImportResult() => new(
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

    private static ImportResult AcceptedImportResultWithElements() => new(
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"),
        [
            new IsolementPivot("C7401-V1", "Vanne 1", "VANNE", "", ""),
            new IsolementPivot("C7401-V2", "Vanne 2", "VANNE", "", ""),
            new IsolementPivot("C7401-V3", "Vanne 3", "VANNE", "", "")
        ],
        [
            new PointPivot("TRAVAUX COMPLET", "38-C7401"),
            new PointPivot("TRAVAUX DETAIL", "38-C7401"),
            new PointPivot("PROLOCK VANNES", "C7401-V1"),
            new PointPivot("PROLOCK VANNES", "C7401-V2"),
            new PointPivot("PROLOCK VANNES", "C7401-V3")
        ],
        [
            new TacheMultiplePivot(1, "Consigner", "ADF", "", "TM_PROC_MAD", null, false, 5),
            new TacheMultiplePivot(2, "Déconsigner", "ADF", "", "TM_PROC_MAD", null, false, 6)
        ],
        []);

    private static ImportResult WarningImportResult() => new(
        new EquipementPivot("38-D8570", "Compresseur D8570", "MAD TRAVAUX"), [], [], [],
        [new ExtractionError("ISOLEMENT", "D8570-V4", ExtractionErrorCode.NoConditionalPointCreated, "VANNE inconnu")]);

    private static ImportResult RejectedImportResult() => new(
        null, [], [], [],
        [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "vide")]);

    private static ProcessOxoFileCommand CreateCommand(
        Guid importProfileId, Guid exportProfileId, IWorkbookReader workbookReader, string sourceFileName = "source.xlsx",
        string? username = null) =>
        new(importProfileId, exportProfileId, workbookReader, sourceFileName, SampleSourceContent, username);

    [Fact]
    public async Task ProcessAsync_WithAcceptedFile_GeneratesArchivesAndReturnsStream()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);

        var generatedWorkbook = new GeneratedWorkbook([]);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(generatedWorkbook);

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var result = await _sut.ProcessAsync(command);

        result.ImportResult.Should().BeSameAs(importResult);
        result.GeneratedFileStream.Should().NotBeNull();
        result.GeneratedFileStream!.Position.Should().Be(0);
        result.GeneratedFileName.Should().StartWith("MAD_38-C7401_").And.EndWith(".xlsx");

        _workbookWriter.Verify(w => w.Write(generatedWorkbook, It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenImportProfileNotFound_ThrowsAndNeverRunsPipeline()
    {
        var importProfileId = Guid.NewGuid();
        _importProfileStore
            .Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportProfile?)null);

        var command = CreateCommand(importProfileId, Guid.NewGuid(), Mock.Of<IWorkbookReader>());

        var act = async () => await _sut.ProcessAsync(command);

        (await act.Should().ThrowAsync<ImportProfileNotFoundException>())
            .Which.ImportProfileId.Should().Be(importProfileId);

        _orchestrator.Verify(o => o.Run(It.IsAny<IWorkbookReader>(), It.IsAny<ImportProfile>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenExportProfileNotFound_ThrowsAndNeverRunsGeneration()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        _importProfileStore
            .Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateImportProfile());
        _exportProfileStore
            .Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExportProfile?)null);

        var command = CreateCommand(importProfileId, exportProfileId, Mock.Of<IWorkbookReader>());

        var act = async () => await _sut.ProcessAsync(command);

        (await act.Should().ThrowAsync<ExportProfileNotFoundException>())
            .Which.ExportProfileId.Should().Be(exportProfileId);

        _orchestrator.Verify(o => o.Run(It.IsAny<IWorkbookReader>(), It.IsAny<ImportProfile>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileIsRejected_ReturnsResultWithoutGenerating()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateExportProfile());

        var rejectedResult = RejectedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(rejectedResult);

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var result = await _sut.ProcessAsync(command);

        result.ImportResult.Should().BeSameAs(rejectedResult);
        result.GeneratedFileStream.Should().BeNull();
        result.GeneratedFileName.Should().BeNull();

        _generationEngine.Verify(e => e.Generate(It.IsAny<ImportResult>(), It.IsAny<ExportProfile>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenPipelineThrows_Rethrows()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateExportProfile());

        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Throws(new InvalidOperationException("corrupt file"));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var act = async () => await _sut.ProcessAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("corrupt file");
    }

    // -- Lot 034: archiving (best-effort, systematic including on rejection) --

    [Fact]
    public async Task ProcessAsync_WhenFileIsRejected_StillArchivesSourceWithRejectedStatusAndNoTarget()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(RejectedImportResult());

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader, "corrompu.xlsx");

        GeneratedFileRecord? saved = null;
        _generatedFileArchiveStore
            .Setup(s => s.SaveAsync(It.IsAny<GeneratedFileRecord>(), It.IsAny<CancellationToken>()))
            .Callback<GeneratedFileRecord, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        await _sut.ProcessAsync(command);

        _generatedFileWriter.Verify(
            w => w.WriteSourceAsync(It.IsAny<Stream>(), "corrompu.xlsx", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _generatedFileWriter.Verify(
            w => w.WriteTargetAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);

        saved.Should().NotBeNull();
        saved!.Status.Should().Be(GeneratedFileArchiveStatus.Rejected);
        saved.EquipementRepere.Should().BeNull();
        saved.TargetFileName.Should().BeNull();
        saved.TargetFilePath.Should().BeNull();
        saved.SourceFileName.Should().Be("corrompu.xlsx");
        saved.ImportProfileId.Should().Be(importProfile.Id);
        saved.ExportProfileId.Should().Be(exportProfile.Id);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileIsAccepted_ArchivesSourceAndTargetWithSuccessStatus()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var result = await _sut.ProcessAsync(command);

        _generatedFileWriter.Verify(
            w => w.WriteSourceAsync(It.IsAny<Stream>(), "source.xlsx", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _generatedFileWriter.Verify(
            w => w.WriteTargetAsync(It.IsAny<Stream>(), "source.xlsx", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(
                It.Is<GeneratedFileRecord>(r =>
                    r.Status == GeneratedFileArchiveStatus.Success
                    && r.EquipementRepere == "38-C7401"
                    && r.TargetFileName == result.GeneratedFileName
                    && r.ImportProfileId == importProfile.Id
                    && r.ExportProfileId == exportProfile.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The stream returned to the caller must still be readable from position 0 -- archiving must
        // not leave it consumed/positioned at the end.
        result.GeneratedFileStream!.Position.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_WithUsernameOnCommand_ArchivesItOnTheRecord()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader, username: "jdupont");

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(It.Is<GeneratedFileRecord>(r => r.Username == "jdupont"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithoutUsernameOnCommand_ArchivesRecordWithNullUsername()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(It.Is<GeneratedFileRecord>(r => r.Username == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileHasNonBlockingWarnings_ArchivesWithNonBlockingWarningStatus()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = WarningImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(
                It.Is<GeneratedFileRecord>(r => r.Status == GeneratedFileArchiveStatus.NonBlockingWarning),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -- Lot 071: element counts on the archived record --

    [Fact]
    public async Task ProcessAsync_WhenFileIsAccepted_ArchivesElementCountsFromTheImportResult()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResultWithElements();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(
                It.Is<GeneratedFileRecord>(r =>
                    r.IsolementCount == 3 && r.PointCount == 5 && r.TacheMultipleCount == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileIsRejected_ArchivesZeroElementCounts()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(RejectedImportResult());

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(
                It.Is<GeneratedFileRecord>(r =>
                    r.IsolementCount == 0 && r.PointCount == 0 && r.TacheMultipleCount == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Anti-hardcoding guard-rail (same principle as EquipementTypeElementNom, Lot C1): a rejected
    // ImportResult with non-empty Isolements/Points/TachesMultiples (impossible in practice per
    // ProcedureExtractionService's own Rejected(...) helper, but constructible here via the mocked
    // orchestrator) must still archive the real counts -- proves TryArchiveAsync genuinely reads
    // .Count rather than special-casing GeneratedFileArchiveStatus.Rejected to 0.
    [Fact]
    public async Task ProcessAsync_WhenRejectedResultCarriesNonEmptyCollections_ArchivesTheirRealCounts()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var artificiallyRejectedResult = new ImportResult(
            null,
            [new IsolementPivot("C7401-V1", "Vanne 1", "VANNE", "", "")],
            [new PointPivot("PROLOCK VANNES", "C7401-V1")],
            [new TacheMultiplePivot(1, "Consigner", "ADF", "", "TM_PROC_MAD", null, false, 5)],
            [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "vide")]);
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(artificiallyRejectedResult);

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        await _sut.ProcessAsync(command);

        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(
                It.Is<GeneratedFileRecord>(r =>
                    r.IsolementCount == 1 && r.PointCount == 1 && r.TacheMultipleCount == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenArchiveStoreThrows_StillReturnsSuccessfulResult()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));
        _generatedFileArchiveStore
            .Setup(s => s.SaveAsync(It.IsAny<GeneratedFileRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var result = await _sut.ProcessAsync(command);

        result.GeneratedFileStream.Should().NotBeNull();
        result.GeneratedFileStream!.Position.Should().Be(0);
        result.GeneratedFileName.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenGeneratedFileWriterThrows_StillReturnsSuccessfulResult()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(exportProfile);

        var importResult = AcceptedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(importResult);
        _generationEngine.Setup(e => e.Generate(importResult, exportProfile)).Returns(new GeneratedWorkbook([]));
        _generatedFileWriter
            .Setup(w => w.WriteSourceAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var command = CreateCommand(importProfileId, exportProfileId, workbookReader);

        var result = await _sut.ProcessAsync(command);

        result.GeneratedFileStream.Should().NotBeNull();
        result.GeneratedFileStream!.Position.Should().Be(0);
        result.GeneratedFileName.Should().NotBeNull();
        _generatedFileArchiveStore.Verify(
            s => s.SaveAsync(It.IsAny<GeneratedFileRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
