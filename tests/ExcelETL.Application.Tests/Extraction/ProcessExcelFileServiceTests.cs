using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction;

public class ProcessExcelFileServiceTests
{
    private readonly Mock<IExtractionConfigRepository> _configRepository = new();
    private readonly Mock<IExcelExtractionService> _extractionService = new();
    private readonly Mock<IExcelGeneratorService> _generatorService = new();
    private readonly Mock<IFileStorageService> _fileStorageService = new();
    private readonly Mock<IExtractionHistoryRepository> _historyRepository = new();
    private readonly ProcessExcelFileService _sut;

    public ProcessExcelFileServiceTests()
    {
        _sut = new ProcessExcelFileService(
            _configRepository.Object,
            _extractionService.Object,
            _generatorService.Object,
            _fileStorageService.Object,
            _historyRepository.Object,
            NullLogger<ProcessExcelFileService>.Instance);

        _historyRepository
            .Setup(r => r.AddAsync(It.IsAny<ExtractionHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _historyRepository
            .Setup(r => r.MarkCompletedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _historyRepository
            .Setup(r => r.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static ExtractionConfig CreateConfig()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);
        return config;
    }

    [Fact]
    public async Task ProcessAsync_WithValidConfig_ExtractsGeneratesSavesAndRecordsCompletedHistory()
    {
        var configId = Guid.NewGuid();
        var config = CreateConfig();
        _configRepository.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var extractionResult = new ExtractionResult([]);
        using var sourceStream = new MemoryStream();
        _extractionService.Setup(s => s.Extract(sourceStream, config)).Returns(extractionResult);

        var generatedStream = new MemoryStream([1, 2, 3]);
        _generatorService.Setup(s => s.Generate(extractionResult)).Returns(generatedStream);

        const string storedPath = @"C:\archive\processed.xlsx";
        _fileStorageService
            .Setup(s => s.SaveAsync(generatedStream, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedPath);

        ExtractionHistory? capturedHistory = null;
        _historyRepository
            .Setup(r => r.AddAsync(It.IsAny<ExtractionHistory>(), It.IsAny<CancellationToken>()))
            .Callback<ExtractionHistory, CancellationToken>((h, _) => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var command = new ProcessExcelFileCommand(configId, sourceStream, "invoice.xlsx");

        var result = await _sut.ProcessAsync(command);

        result.GeneratedFileStream.Should().BeSameAs(generatedStream);
        result.GeneratedFileStream.Position.Should().Be(0);
        result.GeneratedFileName.Should().EndWith(".xlsx");

        capturedHistory.Should().NotBeNull();
        capturedHistory!.SourceFileName.Should().Be("invoice.xlsx");
        capturedHistory.Status.Should().Be(ExtractionStatus.Pending);

        _historyRepository.Verify(
            r => r.MarkCompletedAsync(capturedHistory.Id, storedPath, It.IsAny<CancellationToken>()),
            Times.Once);
        _historyRepository.Verify(
            r => r.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenConfigNotFound_ThrowsAndDoesNotCreateHistory()
    {
        var configId = Guid.NewGuid();
        _configRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractionConfig?)null);

        using var sourceStream = new MemoryStream();
        var command = new ProcessExcelFileCommand(configId, sourceStream, "invoice.xlsx");

        var act = async () => await _sut.ProcessAsync(command);

        (await act.Should().ThrowAsync<ExtractionConfigNotFoundException>())
            .Which.ExtractionConfigId.Should().Be(configId);

        _historyRepository.Verify(
            r => r.AddAsync(It.IsAny<ExtractionHistory>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenExtractionThrows_MarksHistoryFailedAndRethrows()
    {
        var configId = Guid.NewGuid();
        var config = CreateConfig();
        _configRepository.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        using var sourceStream = new MemoryStream();
        _extractionService
            .Setup(s => s.Extract(sourceStream, config))
            .Throws(new InvalidOperationException("corrupt file"));

        ExtractionHistory? capturedHistory = null;
        _historyRepository
            .Setup(r => r.AddAsync(It.IsAny<ExtractionHistory>(), It.IsAny<CancellationToken>()))
            .Callback<ExtractionHistory, CancellationToken>((h, _) => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var command = new ProcessExcelFileCommand(configId, sourceStream, "invoice.xlsx");

        var act = async () => await _sut.ProcessAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("corrupt file");

        capturedHistory.Should().NotBeNull();

        _fileStorageService.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _historyRepository.Verify(
            r => r.MarkFailedAsync(capturedHistory!.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _historyRepository.Verify(
            r => r.MarkCompletedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
