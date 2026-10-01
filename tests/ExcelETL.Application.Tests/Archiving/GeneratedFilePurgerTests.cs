using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Archiving;

public class GeneratedFilePurgerTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IArchiveRetentionSettingsStore> _settings = new();
    private readonly Mock<IGeneratedFileArchiveStore> _store = new();
    private readonly Mock<IGeneratedFileDeleter> _deleter = new();

    private GeneratedFilePurger CreateSut() =>
        new(_settings.Object, _store.Object, _deleter.Object, NullLogger<GeneratedFilePurger>.Instance);

    private static GeneratedFileRecord Record(string? targetPath = "t.xlsx") => new(
        Guid.NewGuid(), NowUtc.AddDays(-100), "C7401", "s.xlsx", "s-path", targetPath is null ? null : "t.xlsx",
        targetPath, Guid.NewGuid(), Guid.NewGuid(), GeneratedFileArchiveStatus.Success);

    [Fact]
    public async Task PurgeExpiredAsync_WithRetentionZero_DoesNothing()
    {
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await CreateSut().PurgeExpiredAsync(NowUtc);

        result.Should().Be(GeneratedFilePurgeResult.None);
        _store.Verify(
            s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeExpiredAsync_UsesTheCutoffFromTheConfiguredDays()
    {
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(90);
        _store.Setup(s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().PurgeExpiredAsync(NowUtc);

        _store.Verify(s => s.GetRecordsWithFilesOlderThanAsync(NowUtc.AddDays(-90), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task PurgeExpiredAsync_DeletesBothFilesAndMarksTheRecord()
    {
        var record = Record();
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(90);
        _store.Setup(s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([record]);
        _deleter.Setup(d => d.Delete("s-path")).Returns(100);
        _deleter.Setup(d => d.Delete("t.xlsx")).Returns(50);

        var result = await CreateSut().PurgeExpiredAsync(NowUtc);

        result.Should().Be(new GeneratedFilePurgeResult(1, 2, 150));
        _store.Verify(s => s.MarkFilesPurgedAsync(record.Id, NowUtc, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task PurgeExpiredAsync_RejectedRecordWithoutTarget_OnlyDeletesTheSource()
    {
        var record = Record(targetPath: null);
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(90);
        _store.Setup(s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([record]);
        _deleter.Setup(d => d.Delete("s-path")).Returns(10);

        var result = await CreateSut().PurgeExpiredAsync(NowUtc);

        result.FilesDeleted.Should().Be(1);
        _deleter.Verify(d => d.Delete(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PurgeExpiredAsync_AMissingFile_StillMarksTheRecordPurged()
    {
        var record = Record();
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(90);
        _store.Setup(s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([record]);
        _deleter.Setup(d => d.Delete(It.IsAny<string>())).Returns(0);

        var result = await CreateSut().PurgeExpiredAsync(NowUtc);

        result.Should().Be(new GeneratedFilePurgeResult(1, 0, 0));
    }

    [Fact]
    public async Task PurgeExpiredAsync_AFailingRecord_IsSkippedAndTheOthersStillPurged()
    {
        var failing = Record();
        var ok = Record();
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(90);
        _store.Setup(s => s.GetRecordsWithFilesOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failing, ok]);
        _deleter.SetupSequence(d => d.Delete(It.IsAny<string>()))
            .Throws(new IOException("locked"))
            .Returns(5).Returns(5);

        var result = await CreateSut().PurgeExpiredAsync(NowUtc);

        result.RecordsPurged.Should().Be(1);
        _store.Verify(s => s.MarkFilesPurgedAsync(failing.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.MarkFilesPurgedAsync(ok.Id, NowUtc, It.IsAny<CancellationToken>()));
    }
}
