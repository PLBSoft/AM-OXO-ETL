using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Home;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Home;

// Lot 054 (54.2): 54.0's own investigation found only GetAllAsync on IImportProfileStore/
// IExportProfileStore (in-memory counting accepted, a few dozen rows) and a dedicated
// GetSummaryAsync on IGeneratedFileArchiveStore (its volume is unbounded, so counting/max are real
// SQL aggregates, not GetAllAsync().Count -- see IGeneratedFileArchiveStore's own comment). Logging
// on a failed read follows this project's established convention (Lot G1): no test asserts on the
// log call itself.
public class HomeIndicatorsServiceTests
{
    private readonly Mock<IImportProfileStore> _importProfileStore = new();
    private readonly Mock<IExportProfileStore> _exportProfileStore = new();
    private readonly Mock<IGeneratedFileArchiveStore> _generatedFileArchiveStore = new();

    private HomeIndicatorsService CreateService() => new(
        _importProfileStore.Object,
        _exportProfileStore.Object,
        _generatedFileArchiveStore.Object,
        NullLogger<HomeIndicatorsService>.Instance);

    private static ImportProfile CreateImportProfile()
    {
        var locator = new RepeatingBlockLocator(
            "SHEET", 1, 1, "Stop", [new BlockFieldDefinition("Stop", "A", 0, 0)]);
        var sheetRule = new SheetExtractionRule("SHEET", locator, [], [], [], []);
        return new ImportProfile(
            "Profile", ImportProfile.DefaultReperePrefix, "MAD TRAVAUX", [], [], [sheetRule]);
    }

    private static ExportProfile CreateExportProfile()
    {
        var sheetRule = new SheetGenerationRule("SHEET", PivotSource.Equipement, [], [], []);
        return new ExportProfile("Profile", [sheetRule]);
    }

    [Fact]
    public async Task GetIndicatorsAsync_WhenEveryStoreSucceeds_ReturnsAllFourValuesAsKnown()
    {
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateImportProfile(), CreateImportProfile()]);
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateExportProfile()]);
        var mostRecent = new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc);
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFileArchiveSummary(5, mostRecent));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.ImportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ImportProfileCount.Value.Should().Be(2);
        indicators.ExportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ExportProfileCount.Value.Should().Be(1);
        indicators.GeneratedFileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.GeneratedFileCount.Value.Should().Be(5);
        indicators.LastGenerationAtUtc.State.Should().Be(HomeIndicatorState.Known);
        indicators.LastGenerationAtUtc.Value.Should().Be(mostRecent);
    }

    [Fact]
    public async Task GetIndicatorsAsync_WithNoProfilesOrGeneratedFiles_CountersAreZero_AndLastGenerationIsAbsent()
    {
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFileArchiveSummary(0, null));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.ImportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ImportProfileCount.Value.Should().Be(0);
        indicators.ExportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ExportProfileCount.Value.Should().Be(0);
        indicators.GeneratedFileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.GeneratedFileCount.Value.Should().Be(0);
        indicators.LastGenerationAtUtc.State.Should().Be(HomeIndicatorState.Absent);
        indicators.LastGenerationAtUtc.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetIndicatorsAsync_WhenImportProfileStoreThrows_MarksOnlyThatIndicatorUnavailable()
    {
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([CreateExportProfile()]);
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFileArchiveSummary(3, DateTime.UtcNow));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.ImportProfileCount.State.Should().Be(HomeIndicatorState.Unavailable);
        indicators.ExportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ExportProfileCount.Value.Should().Be(1);
        indicators.GeneratedFileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.GeneratedFileCount.Value.Should().Be(3);
        indicators.LastGenerationAtUtc.State.Should().Be(HomeIndicatorState.Known);
    }

    [Fact]
    public async Task GetIndicatorsAsync_WhenExportProfileStoreThrows_MarksOnlyThatIndicatorUnavailable()
    {
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([CreateImportProfile()]);
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFileArchiveSummary(0, null));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.ImportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ImportProfileCount.Value.Should().Be(1);
        indicators.ExportProfileCount.State.Should().Be(HomeIndicatorState.Unavailable);
        indicators.GeneratedFileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.LastGenerationAtUtc.State.Should().Be(HomeIndicatorState.Absent);
    }

    [Fact]
    public async Task GetIndicatorsAsync_WhenGeneratedFileArchiveStoreThrows_MarksCountAndLastGenerationUnavailable_OthersRemainKnown()
    {
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([CreateImportProfile()]);
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([CreateExportProfile()]);
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.ImportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.ExportProfileCount.State.Should().Be(HomeIndicatorState.Known);
        indicators.GeneratedFileCount.State.Should().Be(HomeIndicatorState.Unavailable);
        indicators.LastGenerationAtUtc.State.Should().Be(HomeIndicatorState.Unavailable);
    }

    [Fact]
    public async Task GetIndicatorsAsync_LastGenerationDate_ReflectsTheStoreSummarysMostRecentValue_NotInsertionOrder()
    {
        // The store's own aggregate (proven at EfGeneratedFileArchiveStoreTests level) is what
        // determines "most recent" -- this test only proves the service passes that value through
        // unchanged, rather than recomputing or reordering it itself.
        _importProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _exportProfileStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var actualMostRecent = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        _generatedFileArchiveStore.Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFileArchiveSummary(4, actualMostRecent));

        var indicators = await CreateService().GetIndicatorsAsync();

        indicators.LastGenerationAtUtc.Value.Should().Be(actualMostRecent);
    }
}
