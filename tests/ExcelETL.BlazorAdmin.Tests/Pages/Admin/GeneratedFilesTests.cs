using System.Globalization;
using Bunit;
using ExcelETL.Application.Archiving;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.BlazorAdmin.Tests.Pages;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
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
    private readonly Mock<ILocalTimeFormatter> _localTimeFormatterMock = new();
    private readonly string _archiveRoot = Path.Combine(Path.GetTempPath(), "GeneratedFilesTests_" + Guid.NewGuid());

    public GeneratedFilesTests()
    {
        Services.AddSingleton(_archiveStoreMock.Object);
        Services.AddSingleton(_localTimeFormatterMock.Object);
        Services.AddSingleton<IOptions<GeneratedFilesArchiveOptions>>(
            Options.Create(new GeneratedFilesArchiveOptions { RootPath = _archiveRoot }));
        Services.AddLocalization();

        // QuickGrid's OnAfterRenderAsync imports its own JS module (keyboard/scroll wiring, not
        // exercised by any of this page's behavior under test) -- bUnit's JSInterop is strict by
        // default and throws on an unconfigured call, so it's set to Loose here rather than a real
        // page-specific interaction to mock.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Lot 064: see LogsTests.cs's own constructor comment -- same RendererInfo requirement.
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
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
        byte[]? targetBytes = null,
        string? username = null,
        DateTime? generatedAtUtc = null)
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
            generatedAtUtc ?? DateTime.UtcNow,
            equipementRepere,
            "source.xlsx",
            sourceRelativePath,
            targetFileName,
            targetRelativePath,
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            username);
    }

    [Fact]
    public void GeneratedFiles_WithNoRecords_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[]);

        var cut = Render<GeneratedFiles>();

        cut.Markup.Should().Contain("No generated files found.");
    });

    // Lot 042 (42.2): the mobile card's per-row title previously skipped straight from h1 to h5 --
    // fixed to h2, keeping its pre-existing visual size via the Bootstrap `.h5` utility class.
    [Fact]
    public void GeneratedFiles_WithExistingRecord_HasNoHeadingLevelSkip() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
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
        // QuickGrid pads the visible page out to Pagination.ItemsPerPage with empty filler <tr>s
        // (confirmed by decompiling RenderNonVirtualizedRows) so the last page never shrinks the
        // table's height -- only real data rows carry aria-rowindex, filler rows don't.
        table.QuerySelectorAll("tbody tr[aria-rowindex]").Should().HaveCount(3);

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
    // The QuickGrid itself carries no responsive classes of its own -- its wrapping div does, since
    // QuickGrid's Class parameter feeds into its own "quickgrid <Class>" string (confirmed by
    // decompiling the installed package rather than assumed), not additive Bootstrap display utilities.
    [Fact]
    public void GeneratedFiles_RendersBothGridAndCardTemplates_WithResponsiveClasses() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        var gridContainer = cut.Find("#generated-files-grid");
        gridContainer.ClassList.Should().Contain("d-none");
        gridContainer.ClassList.Should().Contain("d-md-block");
        gridContainer.QuerySelector("table.table").Should().NotBeNull();

        var cardContainer = cut.Find("div.d-md-none");
        cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
    });

    [Fact]
    public void GeneratedFiles_UsernameColumn_DisplaysValueInTableAndCard() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles(
            "C7401", GeneratedFileArchiveStatus.Success, withTarget: true, username: "J.DUPONT");
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        cut.Find("table.table tbody").TextContent.Should().Contain("J.DUPONT");
        cut.Find("div.d-md-none .card").TextContent.Should().Contain("J.DUPONT");
    });

    [Fact]
    public void GeneratedFiles_UsernameColumn_ShowsPlaceholder_WhenNull() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles(
            "C7401", GeneratedFileArchiveStatus.Success, withTarget: true, username: null);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        cut.Find("table.table tbody tr").TextContent.Should().Contain("—");
        cut.Find("div.d-md-none .card").TextContent.Should().Contain("—");
    });

    // QuickGrid's own header markup (button.col-title, decompiled from the installed package) is
    // trusted as-is -- this asserts our column wiring (SortBy) actually reorders the rows, not
    // QuickGrid's own already-tested sorting mechanism.
    [Fact]
    public void GeneratedFiles_Grid_SortsByEquipementRepereColumn_OnHeaderClick() => WithCulture("en-US", () =>
    {
        var b = WriteRecordWithRealFiles("B-Site", GeneratedFileArchiveStatus.Success, withTarget: true);
        var a = WriteRecordWithRealFiles("A-Site", GeneratedFileArchiveStatus.Success, withTarget: true);
        var c = WriteRecordWithRealFiles("C-Site", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[b, a, c]);

        var cut = Render<GeneratedFiles>();

        // Column order: Date, Equipment reference, User, Status -- the only 4 sortable headers.
        var repereHeaderButton = cut.FindAll("button.col-title")[1];
        repereHeaderButton.Click();

        var rows = cut.FindAll("table.table tbody tr[aria-rowindex]");
        rows[0].TextContent.Should().Contain("A-Site");
        rows[1].TextContent.Should().Contain("B-Site");
        rows[2].TextContent.Should().Contain("C-Site");
    });

    [Fact]
    public void GeneratedFiles_Grid_PaginatesAtTwentyFiveItemsPerPage_WithWorkingNextButton() => WithCulture("en-US", () =>
    {
        var records = Enumerable.Range(1, 30)
            .Select(i => WriteRecordWithRealFiles(
                $"Site-{i:00}", GeneratedFileArchiveStatus.Success, withTarget: true,
                generatedAtUtc: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)records);

        var cut = Render<GeneratedFiles>();

        // Real data rows only -- QuickGrid pads the rest of the page out to ItemsPerPage with
        // empty filler <tr>s (no aria-rowindex), confirmed by decompiling RenderNonVirtualizedRows.
        cut.FindAll("table.table tbody tr[aria-rowindex]").Should().HaveCount(25);

        cut.Find("#generated-files-paginator button.go-next").Click();

        cut.FindAll("table.table tbody tr[aria-rowindex]").Should().HaveCount(5);
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

    // Lot 064 (64.2): same stable-id/UTC-fallback + interactive-conversion mechanism as Logs.razor,
    // extended to the scope this ticket was widened to (see the ticket doc's 64.0 investigation).
    [Fact]
    public void GeneratedFileDateCells_HaveStableIds_AndShowUtcFallback_WhenNotYetInteractive() => WithCulture("en-US", () =>
    {
        var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
        _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);

        var cut = Render<GeneratedFiles>();

        var tableCell = cut.Find($"#generated-file-date-{record.Id}");
        var cardCell = cut.Find($"#generated-file-date-card-{record.Id}");
        tableCell.TextContent.Should().Be(record.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm:ss"));
        cardCell.TextContent.Should().Be(tableCell.TextContent);
    });

    [Fact]
    public void GeneratedFileDateCells_UpdateToTheLocalTimeFormatterResult_OnceInteractive() =>
        WithCulture("en-US", () =>
        {
            var record = WriteRecordWithRealFiles("C7401", GeneratedFileArchiveStatus.Success, withTarget: true);
            _archiveStoreMock.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);
            _localTimeFormatterMock
                .Setup(f => f.FormatManyAsync(It.IsAny<IReadOnlyList<DateTime>>(), "dd/MM/yyyy HH:mm:ss"))
                .ReturnsAsync((IReadOnlyList<DateTime> values, string _) =>
                    values.Select(_ => "STUBBED-LOCAL-TIME").ToList());
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));

            var cut = Render<GeneratedFiles>();
            cut.WaitForState(() => cut.Find($"#generated-file-date-{record.Id}").TextContent == "STUBBED-LOCAL-TIME");

            cut.Find($"#generated-file-date-card-{record.Id}").TextContent.Should().Be("STUBBED-LOCAL-TIME");
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
