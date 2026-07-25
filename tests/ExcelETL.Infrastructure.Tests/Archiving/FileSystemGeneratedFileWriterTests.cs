using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Archiving;

public class FileSystemGeneratedFileWriterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "GeneratedFilesArchiveTests_" + Guid.NewGuid());

    [Fact]
    public async Task WriteSourceAsync_WritesFilePhysicallyAtReturnedPath_WithIdenticalContent()
    {
        var writer = CreateWriter();
        var timestampUtc = new DateTime(2026, 7, 25, 14, 30, 0, 123, DateTimeKind.Utc);
        using var content = new MemoryStream([1, 2, 3, 4, 5]);

        var relativePath = await writer.WriteSourceAsync(content, "Dossier_C7401.xlsx", timestampUtc);

        relativePath.Should().Be(Path.Combine("2026", "07", "20260725-143000-123_source_Dossier_C7401.xlsx"));
        var fullPath = Path.Combine(_rootPath, relativePath);
        File.Exists(fullPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(fullPath)).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task WriteTargetAsync_WritesFilePhysicallyAtReturnedPath_WithIdenticalContent()
    {
        var writer = CreateWriter();
        var timestampUtc = new DateTime(2026, 7, 25, 9, 5, 7, 9, DateTimeKind.Utc);
        using var content = new MemoryStream([9, 8, 7]);

        var relativePath = await writer.WriteTargetAsync(content, "Dossier_D8570.xlsx", timestampUtc);

        relativePath.Should().Be(Path.Combine("2026", "07", "20260725-090507-009_target_Dossier_D8570.xlsx"));
        var fullPath = Path.Combine(_rootPath, relativePath);
        File.Exists(fullPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(fullPath)).Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task WriteSourceAsync_CalledTwiceAtSameMillisecondWithDifferentOriginalNames_ProducesTwoDistinctFiles()
    {
        var writer = CreateWriter();
        var timestampUtc = new DateTime(2026, 7, 25, 14, 30, 0, 123, DateTimeKind.Utc);

        using var contentA = new MemoryStream([1]);
        var pathA = await writer.WriteSourceAsync(contentA, "A.xlsx", timestampUtc);
        using var contentB = new MemoryStream([2]);
        var pathB = await writer.WriteSourceAsync(contentB, "B.xlsx", timestampUtc);

        pathA.Should().NotBe(pathB);
        File.Exists(Path.Combine(_rootPath, pathA)).Should().BeTrue();
        File.Exists(Path.Combine(_rootPath, pathB)).Should().BeTrue();
    }

    [Fact]
    public async Task WriteSourceAsync_CalledTwiceWithSameOriginalNameAndExactSameMillisecond_OverwritesWithoutThrowing()
    {
        // Characterizes the real (accepted) behavior rather than imposing a non-acted guarantee -- see
        // docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md, 34.3.
        var writer = CreateWriter();
        var timestampUtc = new DateTime(2026, 7, 25, 14, 30, 0, 123, DateTimeKind.Utc);

        using var first = new MemoryStream([1, 1, 1]);
        var firstPath = await writer.WriteSourceAsync(first, "Same.xlsx", timestampUtc);
        using var second = new MemoryStream([2, 2]);
        var secondPath = await writer.WriteSourceAsync(second, "Same.xlsx", timestampUtc);

        secondPath.Should().Be(firstPath);
        (await File.ReadAllBytesAsync(Path.Combine(_rootPath, secondPath))).Should().Equal(2, 2);
    }

    [Fact]
    public async Task WriteSourceAsync_WithForbiddenWindowsCharacterInOriginalName_ReplacesItAndSucceeds()
    {
        var writer = CreateWriter();
        var timestampUtc = new DateTime(2026, 7, 25, 14, 30, 0, 0, DateTimeKind.Utc);
        using var content = new MemoryStream([1]);

        var relativePath = await writer.WriteSourceAsync(content, "dossier:test.xlsx", timestampUtc);

        Path.GetFileName(relativePath).Should().Be("20260725-143000-000_source_dossier_test.xlsx");
        File.Exists(Path.Combine(_rootPath, relativePath)).Should().BeTrue();
    }

    [Fact]
    public async Task WriteSourceAsync_WithMissingYearMonthSubdirectory_CreatesItAutomatically()
    {
        var writer = CreateWriter();
        Directory.Exists(_rootPath).Should().BeFalse();
        var timestampUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        using var content = new MemoryStream([1]);

        var relativePath = await writer.WriteSourceAsync(content, "source.xlsx", timestampUtc);

        Directory.Exists(Path.Combine(_rootPath, "2026", "01")).Should().BeTrue();
        File.Exists(Path.Combine(_rootPath, relativePath)).Should().BeTrue();
    }

    [Fact]
    public async Task WriteSourceAsync_WithMissingRootPathConfiguration_ThrowsInvalidOperationException()
    {
        var writer = new FileSystemGeneratedFileWriter(Options.Create(new GeneratedFilesArchiveOptions { RootPath = "" }));
        using var content = new MemoryStream();

        var act = async () => await writer.WriteSourceAsync(content, "source.xlsx", DateTime.UtcNow);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private FileSystemGeneratedFileWriter CreateWriter() =>
        new(Options.Create(new GeneratedFilesArchiveOptions { RootPath = _rootPath }));

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
