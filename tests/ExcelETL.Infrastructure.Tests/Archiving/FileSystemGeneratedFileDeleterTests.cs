using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Archiving;

public class FileSystemGeneratedFileDeleterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "deleter-" + Guid.NewGuid());

    public FileSystemGeneratedFileDeleterTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileSystemGeneratedFileDeleter CreateSut() =>
        new(Options.Create(new GeneratedFilesArchiveOptions { RootPath = _root }));

    [Fact]
    public void Delete_AnExistingFile_RemovesItAndReturnsItsSize()
    {
        Directory.CreateDirectory(Path.Combine(_root, "2026", "07"));
        var relative = Path.Combine("2026", "07", "a.xlsx");
        File.WriteAllBytes(Path.Combine(_root, relative), new byte[123]);

        CreateSut().Delete(relative).Should().Be(123);

        File.Exists(Path.Combine(_root, relative)).Should().BeFalse();
    }

    [Fact]
    public void Delete_AMissingFile_ReturnsZeroWithoutThrowing()
    {
        CreateSut().Delete(Path.Combine("2026", "missing.xlsx")).Should().Be(0);
    }

    [Fact]
    public void Delete_APathEscapingTheRoot_ThrowsAndLeavesTheOutsideFileAlone()
    {
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid() + ".xlsx");
        File.WriteAllBytes(outside, [1, 2, 3]);
        try
        {
            var act = () => CreateSut().Delete(Path.Combine("..", Path.GetFileName(outside)));

            act.Should().Throw<InvalidOperationException>();
            File.Exists(outside).Should().BeTrue();
        }
        finally
        {
            File.Delete(outside);
        }
    }
}
