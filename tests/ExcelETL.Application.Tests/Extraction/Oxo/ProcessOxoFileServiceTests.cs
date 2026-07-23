using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Generation;
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
    private readonly Mock<IImportProfileStore> _importProfileStore = new();
    private readonly Mock<IExportProfileStore> _exportProfileStore = new();
    private readonly Mock<IImportPipelineOrchestrator> _orchestrator = new();
    private readonly Mock<ISheetGenerationEngine> _generationEngine = new();
    private readonly Mock<IWorkbookWriter> _workbookWriter = new();
    private readonly Mock<IFileStorageService> _fileStorageService = new();
    private readonly ProcessOxoFileService _sut;

    public ProcessOxoFileServiceTests()
    {
        _sut = new ProcessOxoFileService(
            _importProfileStore.Object,
            _exportProfileStore.Object,
            _orchestrator.Object,
            _generationEngine.Object,
            _workbookWriter.Object,
            _fileStorageService.Object,
            NullLogger<ProcessOxoFileService>.Instance);
    }

    private static ImportProfile CreateImportProfile() => new(
        "Profil import test", "MAD TRAVAUX", [], [],
        [new SheetExtractionRule(
            "PROCEDURE",
            new RepeatingBlockLocator("PROCEDURE", 1, 1, "Stop", [new BlockFieldDefinition("Stop", "A", 0, 0)]),
            [],
            [])]);

    private static ExportProfile CreateExportProfile() => new(
        "Profil export test",
        [new SheetGenerationRule(
            "Parents", PivotSource.Equipement, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)], [])]);

    private static ImportResult AcceptedImportResult() => new(
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

    private static ImportResult RejectedImportResult() => new(
        null, [], [], [],
        [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "vide")]);

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

        const string storedPath = @"C:\archive\MAD_38-C7401.xlsx";
        _fileStorageService
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedPath);

        var command = new ProcessOxoFileCommand(importProfileId, exportProfileId, workbookReader, "source.xlsx");

        var result = await _sut.ProcessAsync(command);

        result.ImportResult.Should().BeSameAs(importResult);
        result.GeneratedFileStream.Should().NotBeNull();
        result.GeneratedFileStream!.Position.Should().Be(0);
        result.GeneratedFileName.Should().StartWith("MAD_38-C7401_").And.EndWith(".xlsx");

        _workbookWriter.Verify(w => w.Write(generatedWorkbook, It.IsAny<Stream>()), Times.Once);
        _fileStorageService.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), result.GeneratedFileName!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenImportProfileNotFound_ThrowsAndNeverRunsPipeline()
    {
        var importProfileId = Guid.NewGuid();
        _importProfileStore
            .Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportProfile?)null);

        var command = new ProcessOxoFileCommand(importProfileId, Guid.NewGuid(), Mock.Of<IWorkbookReader>(), "source.xlsx");

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

        var command = new ProcessOxoFileCommand(importProfileId, exportProfileId, Mock.Of<IWorkbookReader>(), "source.xlsx");

        var act = async () => await _sut.ProcessAsync(command);

        (await act.Should().ThrowAsync<ExportProfileNotFoundException>())
            .Which.ExportProfileId.Should().Be(exportProfileId);

        _orchestrator.Verify(o => o.Run(It.IsAny<IWorkbookReader>(), It.IsAny<ImportProfile>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileIsRejected_ReturnsResultWithoutGeneratingOrArchiving()
    {
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();
        var importProfile = CreateImportProfile();
        _importProfileStore.Setup(s => s.GetByIdAsync(importProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(importProfile);
        _exportProfileStore.Setup(s => s.GetByIdAsync(exportProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateExportProfile());

        var rejectedResult = RejectedImportResult();
        var workbookReader = Mock.Of<IWorkbookReader>();
        _orchestrator.Setup(o => o.Run(workbookReader, importProfile)).Returns(rejectedResult);

        var command = new ProcessOxoFileCommand(importProfileId, exportProfileId, workbookReader, "source.xlsx");

        var result = await _sut.ProcessAsync(command);

        result.ImportResult.Should().BeSameAs(rejectedResult);
        result.GeneratedFileStream.Should().BeNull();
        result.GeneratedFileName.Should().BeNull();

        _generationEngine.Verify(e => e.Generate(It.IsAny<ImportResult>(), It.IsAny<ExportProfile>()), Times.Never);
        _fileStorageService.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var command = new ProcessOxoFileCommand(importProfileId, exportProfileId, workbookReader, "source.xlsx");

        var act = async () => await _sut.ProcessAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("corrupt file");
    }
}
