using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Entities;

public class ExtractionHistoryTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesHistoryEntryInPendingStatus()
    {
        var jobTimestamp = DateTimeOffset.UtcNow;

        var history = new ExtractionHistory(jobTimestamp, "invoice-2026-001.xlsx");

        history.Id.Should().NotBeEmpty();
        history.JobTimestamp.Should().Be(jobTimestamp);
        history.SourceFileName.Should().Be("invoice-2026-001.xlsx");
        history.Status.Should().Be(ExtractionStatus.Pending);
        history.StoredFilePath.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSourceFileName_ThrowsArgumentException(string? invalidFileName)
    {
        var act = () => new ExtractionHistory(DateTimeOffset.UtcNow, invalidFileName!);

        act.Should().Throw<ArgumentException>().WithParameterName("sourceFileName");
    }

    [Fact]
    public void MarkCompleted_WithValidStoredFilePath_UpdatesStatusAndPath()
    {
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice-2026-001.xlsx");

        history.MarkCompleted(@"C:\archive\invoice-2026-001-processed.xlsx");

        history.Status.Should().Be(ExtractionStatus.Completed);
        history.StoredFilePath.Should().Be(@"C:\archive\invoice-2026-001-processed.xlsx");
    }

    [Fact]
    public void MarkFailed_UpdatesStatusToFailed()
    {
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice-2026-001.xlsx");

        history.MarkFailed();

        history.Status.Should().Be(ExtractionStatus.Failed);
        history.StoredFilePath.Should().BeNull();
    }

    [Fact]
    public void MarkCompleted_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice-2026-001.xlsx");
        history.MarkCompleted(@"C:\archive\file.xlsx");

        var act = () => history.MarkCompleted(@"C:\archive\file2.xlsx");

        act.Should().Throw<InvalidOperationException>();
    }
}
