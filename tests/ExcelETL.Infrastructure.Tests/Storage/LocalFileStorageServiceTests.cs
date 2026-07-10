using ExcelETL.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Storage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _archiveDirectory =
        Path.Combine(Path.GetTempPath(), "ExcelEtlTests_" + Guid.NewGuid());

    [Fact]
    public async Task SaveAsync_WritesFileToArchiveDirectoryAndReturnsFullPath()
    {
        var service = new LocalFileStorageService(
            Options.Create(new FileStorageOptions { ArchiveDirectory = _archiveDirectory }));
        using var content = new MemoryStream([1, 2, 3, 4]);

        var storedPath = await service.SaveAsync(content, "processed.xlsx");

        storedPath.Should().Be(Path.Combine(_archiveDirectory, "processed.xlsx"));
        File.Exists(storedPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(storedPath)).Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }

    [Fact]
    public async Task SaveAsync_WithMissingArchiveDirectoryConfiguration_ThrowsInvalidOperationException()
    {
        var service = new LocalFileStorageService(
            Options.Create(new FileStorageOptions { ArchiveDirectory = "" }));
        using var content = new MemoryStream();

        var act = async () => await service.SaveAsync(content, "processed.xlsx");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_archiveDirectory))
        {
            Directory.Delete(_archiveDirectory, recursive: true);
        }
    }
}
