using ExcelETL.BlazorAdmin.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Excel;

// Lot 033: pure unit tests for the shared batch validator, no bUnit rendering needed. FakeBrowserFile
// lets Size be set directly, without allocating real (possibly huge) byte content -- important for
// the total-size boundary cases below.
public class BatchFileValidatorTests
{
    private const long OneMb = 1024 * 1024;

    private sealed class FakeBrowserFile(string name, long size) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size { get; } = size;
        public string ContentType => "application/octet-stream";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed for validator tests.");
    }

    [Fact]
    public void ValidateCount_WithExactlyTheLimit_ReturnsNull() =>
        BatchFileValidator.ValidateCount(BatchUploadLimits.MaxFilesPerBatch).Should().BeNull();

    [Fact]
    public void ValidateCount_OneOverTheLimit_ReturnsTooManyFilesFailure()
    {
        var failure = BatchFileValidator.ValidateCount(BatchUploadLimits.MaxFilesPerBatch + 1);

        failure.Should().NotBeNull();
        failure!.Reason.Should().Be(BatchValidationFailureReason.TooManyFiles);
        failure.SelectedFileCount.Should().Be(BatchUploadLimits.MaxFilesPerBatch + 1);
    }

    [Fact]
    public void ValidateSizes_WithFileExactlyAtTheLimit_ReturnsNull()
    {
        var files = new List<IBrowserFile> { new FakeBrowserFile("a.xlsx", BatchUploadLimits.MaxFileSizeBytes) };

        BatchFileValidator.ValidateSizes(files).Should().BeNull();
    }

    [Fact]
    public void ValidateSizes_WithOneFileOverTheLimit_ReturnsFileTooLargeFailure_NamingIt()
    {
        var oversized = new FakeBrowserFile("big.xlsx", BatchUploadLimits.MaxFileSizeBytes + 1);
        var files = new List<IBrowserFile> { new FakeBrowserFile("ok.xlsx", OneMb), oversized };

        var failure = BatchFileValidator.ValidateSizes(files);

        failure.Should().NotBeNull();
        failure!.Reason.Should().Be(BatchValidationFailureReason.FileTooLarge);
        failure.OversizedFiles.Should().ContainSingle().Which.Should().BeSameAs(oversized);
    }

    [Fact]
    public void ValidateSizes_WithMultipleFilesOverTheLimit_ListsAllOfThem()
    {
        var first = new FakeBrowserFile("big1.xlsx", BatchUploadLimits.MaxFileSizeBytes + 1);
        var second = new FakeBrowserFile("big2.xlsx", BatchUploadLimits.MaxFileSizeBytes + OneMb);
        var files = new List<IBrowserFile> { first, second };

        var failure = BatchFileValidator.ValidateSizes(files);

        failure!.OversizedFiles.Should().BeEquivalentTo([first, second]);
    }

    // The ticket's own explicit note: 20 files x 10 MB = exactly 200 MB, so this branch is not
    // reachable through the real UI (count/individual-size checks would fire first) -- verified
    // here directly against the validator, bypassing the count constraint on purpose, per the
    // ticket's own suggested workaround.
    [Fact]
    public void ValidateSizes_WithTotalOverTheLimit_ButNoSingleFileOversized_ReturnsTotalSizeTooLargeFailure()
    {
        var files = Enumerable.Range(0, 21)
            .Select(i => (IBrowserFile)new FakeBrowserFile($"f{i}.xlsx", BatchUploadLimits.MaxFileSizeBytes))
            .ToList();

        var failure = BatchFileValidator.ValidateSizes(files);

        failure.Should().NotBeNull();
        failure!.Reason.Should().Be(BatchValidationFailureReason.TotalSizeTooLarge);
        failure.TotalSizeBytes.Should().Be(21L * BatchUploadLimits.MaxFileSizeBytes);
        failure.OversizedFiles.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSizes_WithTotalExactlyAtTheLimit_ReturnsNull()
    {
        var files = Enumerable.Range(0, BatchUploadLimits.MaxFilesPerBatch)
            .Select(i => (IBrowserFile)new FakeBrowserFile($"f{i}.xlsx", BatchUploadLimits.MaxFileSizeBytes))
            .ToList();

        files.Sum(f => f.Size).Should().Be(BatchUploadLimits.MaxTotalBatchSizeBytes);
        BatchFileValidator.ValidateSizes(files).Should().BeNull();
    }

    [Theory]
    [InlineData(10 * 1024 * 1024, "10")]
    [InlineData(11 * 1024 * 1024, "11")]
    [InlineData(1024 * 1024, "1")]
    public void FormatMegabytes_FormatsWholeNumbers_WithoutTrailingDecimals(long bytes, string expected) =>
        BatchFileValidator.FormatMegabytes(bytes).Should().Be(expected);

    [Fact]
    public void FormatMegabytes_FormatsFractionalValues_WithUpToTwoDecimals() =>
        BatchFileValidator.FormatMegabytes((long)(10.5 * 1024 * 1024)).Should().Be("10.5");
}
