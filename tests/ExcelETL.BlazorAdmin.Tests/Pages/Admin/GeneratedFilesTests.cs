using System.Globalization;
using Bunit;
using ExcelETL.Application.Archiving;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// 34.5: real files on a temp directory back the download tests (never a filesystem mock), same
// convention already established for FileSystemGeneratedFileWriterTests (34.3).
public class GeneratedFilesTests : BunitContext
{
    private readonly Mock<IGeneratedFileArchiveStore> _archiveStoreMock = new();
    private readonly string _archiveRoot = Path.Combine(Path.GetTempPath(), "GeneratedFilesTests_" + Guid.NewGuid());

    public GeneratedFilesTests()
    {
        Services.AddSingleton(_archiveStoreMock.Object);
        Services.AddSingleton<IOptions<GeneratedFilesArchiveOptions>>(
            Options.Create(new GeneratedFilesArchiveOptions { RootPath = _archiveRoot }));
        Services.AddLocalization();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private GeneratedFileRecord WriteRecordWithRealFiles(
        string? equipementRepere,
        GeneratedFileArchiveStatus status,
        bool withTarget,
        byte[]? sourceBytes = null,
        byte[]? targetBytes = null)
    {
        Directory.CreateDirectory(_archiveRoot);
        var sourceRelativePath = $"{Guid.NewGuid()}_source.xlsx";
        File.WriteAllBytes(Path.Combine(_archiveRoot, sourceRelativePath), sourceBytes ?? [1, 2, 3]);

        string? targetRelativePath = null;
        string? targetFileName = null;
        if (withTarget)
        {
            targetRelativePath = $"{Guid.NewGuid()}_target.xlsx";
            File.WriteAllBytes(Path.Combine(_archiveRoot, targetRelativePath), targetBytes ?? [4, 5, 6]);
            targetFileName = "generated.xlsx";
        }

        return new GeneratedFileRecord(
            Guid.NewGuid(),
            DateTime.UtcNow,
            equipementRepere,
            "source.xlsx",
            sourceRelativePath,
            targetFileName,
            targetRelativePath,
            Guid.NewGuid(),
            Guid.NewGuid(),
            status);
    }

    [Fact]
    public void GeneratedFiles_WithNoRecords_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[]);

        var cut = Render<GeneratedFiles>();

        cut.Markup.Should().Contain("No generated files found.");
    });

    [Fact]
    public void GeneratedFiles_WithThreeRecordsOfVariedStatus_RendersThreeRowsWithCorrectBadges() => WithCulture("en-US", () =>
    {
        var success = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        var warning = WriteRecordWithRealFiles("D8570", GeneratedFileArchiveStatus.NonBlockingWarning, withTarget: true);
        var rejected = WriteRecordWithRealFiles(null, GeneratedFileArchiveStatus.Rejected, withTarget: false);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[success, warning, rejected]);

        var cut = Render<GeneratedFiles>();

        var table = cut.Find("table.table");
        table.QuerySelectorAll("tbody tr").Should().HaveCount(3);

        var badges = table.QuerySelectorAll(".badge");
        badges.Should().Contain(b => b.ClassList.Contains("bg-success"));
        badges.Should().Contain(b => b.ClassList.Contains("bg-warning"));
        badges.Should().Contain(b => b.ClassList.Contains("bg-danger"));
    });

    [Fact]
    public void GeneratedFiles_RejectedRecordWithoutTarget_HasNoTargetDownloadButtonAndShowsPlaceholderRepere() => WithCulture("en-US", () =>
    {
        var rejected = WriteRecordWithRealFiles(null, GeneratedFileArchiveStatus.Rejected, withTarget: false);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[rejected]);

        var cut = Render<GeneratedFiles>();

        cut.FindAll($"#prepare-download-target-button-{rejected.Id}").Should().BeEmpty();
        cut.FindAll($"#download-target-link-{rejected.Id}").Should().BeEmpty();
        cut.FindAll($"#prepare-download-source-button-{rejected.Id}").Should().HaveCount(1);
        cut.Find("table.table tbody tr td:nth-child(2)").TextContent.Should().Contain("—");
    });

    [Fact]
    public void GeneratedFiles_SearchButton_CallsSearchAsyncWithEnteredTerm_AndUpdatesListFromMockResult() => WithCulture("en-US", () =>
    {
        var initial = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        var filtered = WriteRecordWithRealFiles("D8570", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[initial]);
        _archiveStoreMock.Setup(s => s.SearchAsync("D8570", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[filtered]);

        var cut = Render<GeneratedFiles>();
        cut.Find("table.table tbody").TextContent.Should().Contain("C7401");

        cut.Find("#generated-files-search-input").Input("D8570");
        cut.Find("#generated-files-search-button").Click();

        _archiveStoreMock.Verify(s => s.SearchAsync("D8570", It.IsAny<CancellationToken>()), Times.Once);
        cut.Find("table.table tbody").TextContent.Should().Contain("D8570");
        cut.Find("table.table tbody").TextContent.Should().NotContain("C7401");
    });

    [Fact]
    public void GeneratedFiles_ClearSearchButton_ReloadsFullListViaSearchAsyncWithNullTerm() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();
        cut.Find("#generated-files-search-input").Input("anything");
        cut.Find("#generated-files-clear-search-button").Click();

        _archiveStoreMock.Verify(s => s.SearchAsync(null, It.IsAny<CancellationToken>()), Times.AtLeast(2));
        cut.Find("#generated-files-search-input").GetAttribute("value").Should().BeEmpty();
    });

    [Fact]
    public void GeneratedFiles_ClickPrepareSourceDownload_RendersLinkWithBase64EncodedFileContent() => WithCulture("en-US", () =>
    {
        var sourceBytes = new byte[] { 10, 20, 30 };
        var record = WriteRecordWithRealFiles(
            "C7401", GeneratedFileArchiveStatus.Success, withTarget: false, sourceBytes: sourceBytes);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();
        cut.Find($"#prepare-download-source-button-{record.Id}").Click();

        var link = cut.Find($"#download-source-link-{record.Id}");
        link.GetAttribute("href").Should().Be(
            $"data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{Convert.ToBase64String(sourceBytes)}");
        link.GetAttribute("download").Should().Be(record.SourceFileName);
    });

    [Fact]
    public void GeneratedFiles_ClickPrepareTargetDownload_RendersLinkWithBase64EncodedFileContent() => WithCulture("en-US", () =>
    {
        var targetBytes = new byte[] { 40, 50, 60 };
        var record = WriteRecordWithRealFiles(
            "C7401", GeneratedFileArchiveStatus.Success, withTarget: true, targetBytes: targetBytes);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();
        cut.Find($"#prepare-download-target-button-{record.Id}").Click();

        var link = cut.Find($"#download-target-link-{record.Id}");
        link.GetAttribute("href").Should().Be(
            $"data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{Convert.ToBase64String(targetBytes)}");
        link.GetAttribute("download").Should().Be(record.TargetFileName);
    });

    // V2: mobile-first table -> card fallback at the md breakpoint, same idiom as ImportProfiles/Users.
    [Fact]
    public void GeneratedFiles_RendersBothTableAndCardTemplates_WithResponsiveClasses() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        var table = cut.Find("table.table");
        table.ClassList.Should().Contain("d-none");
        table.ClassList.Should().Contain("d-md-table");

        var cardContainer = cut.Find("div.d-md-none");
        cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
    });

    [Fact]
    public void GeneratedFiles_CardTemplate_ClickPrepareSourceDownload_RendersLinkWithDistinctCardId() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: false);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();
        cut.Find($"#prepare-download-source-button-card-{record.Id}").Click();

        cut.FindAll($"#download-source-link-card-{record.Id}").Should().HaveCount(1);
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_archiveRoot))
        {
            Directory.Delete(_archiveRoot, recursive: true);
        }

        base.Dispose(disposing);
    }
}
