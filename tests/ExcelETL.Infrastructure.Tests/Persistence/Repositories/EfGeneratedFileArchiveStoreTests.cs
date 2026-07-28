using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class EfGeneratedFileArchiveStoreTests
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("EfGeneratedFileArchiveStoreTests_" + Guid.NewGuid());

    private IGeneratedFileArchiveStore CreateStore() => new EfGeneratedFileArchiveStore(_dbContextFactory);

    private static GeneratedFileRecord CreateRecord(
        DateTime generatedAtUtc,
        string? equipementRepere = "C7401",
        GeneratedFileArchiveStatus status = GeneratedFileArchiveStatus.Success,
        string? targetFileName = "MAD_C7401_20260725143000.xlsx",
        string? targetFilePath = @"2026\07\20260725-143000-000_target_source.xlsx") => new(
        Guid.NewGuid(),
        generatedAtUtc,
        equipementRepere,
        "source.xlsx",
        @"2026\07\20260725-143000-000_source_source.xlsx",
        targetFileName,
        targetFilePath,
        Guid.NewGuid(),
        Guid.NewGuid(),
        status);

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_RoundTripsAllPropertiesIdentically()
    {
        var record = CreateRecord(new DateTime(2026, 7, 25, 14, 30, 0, DateTimeKind.Utc));
        var store = CreateStore();

        await store.SaveAsync(record);
        var reloaded = await store.GetByIdAsync(record.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(record.Id);
        reloaded.GeneratedAtUtc.Should().Be(record.GeneratedAtUtc);
        reloaded.EquipementRepere.Should().Be(record.EquipementRepere);
        reloaded.SourceFileName.Should().Be(record.SourceFileName);
        reloaded.SourceFilePath.Should().Be(record.SourceFilePath);
        reloaded.TargetFileName.Should().Be(record.TargetFileName);
        reloaded.TargetFilePath.Should().Be(record.TargetFilePath);
        reloaded.ImportProfileId.Should().Be(record.ImportProfileId);
        reloaded.ExportProfileId.Should().Be(record.ExportProfileId);
        reloaded.Status.Should().Be(record.Status);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_WithRejectedCase_RoundTripsNullFieldsAsNull()
    {
        var record = CreateRecord(
            DateTime.UtcNow, equipementRepere: null, status: GeneratedFileArchiveStatus.Rejected,
            targetFileName: null, targetFilePath: null);
        var store = CreateStore();

        await store.SaveAsync(record);
        var reloaded = await store.GetByIdAsync(record.Id);

        reloaded.Should().NotBeNull();
        reloaded!.EquipementRepere.Should().BeNull();
        reloaded.TargetFileName.Should().BeNull();
        reloaded.TargetFilePath.Should().BeNull();
        reloaded.Status.Should().Be(GeneratedFileArchiveStatus.Rejected);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var store = CreateStore();

        var result = await store.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_WithoutFilter_ReturnsAllRecordsOrderedByGeneratedAtUtcDescending()
    {
        var store = CreateStore();
        var oldest = CreateRecord(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var middle = CreateRecord(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));
        var newest = CreateRecord(new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc));
        await store.SaveAsync(oldest);
        await store.SaveAsync(newest);
        await store.SaveAsync(middle);

        var results = await store.SearchAsync(null);

        results.Select(r => r.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task SearchAsync_WithLowercaseTerm_FindsRecordWithDifferentCasingRepere()
    {
        var store = CreateStore();
        var record = CreateRecord(DateTime.UtcNow, equipementRepere: "C7401");
        await store.SaveAsync(record);

        var results = await store.SearchAsync("c7401");

        results.Should().ContainSingle().Which.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task SearchAsync_WithAbsentRepere_ReturnsEmptyListWithoutThrowing()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateRecord(DateTime.UtcNow, equipementRepere: "C7401"));

        var results = await store.SearchAsync("UNKNOWN-REPERE");

        results.Should().BeEmpty();
    }

    // Lot 054 (54.0/54.2): the home page's generated-files KPI tile backs onto this method directly,
    // rather than SearchAsync(null).Count -- a real SQL aggregate, never an in-memory row scan.
    [Fact]
    public async Task GetSummaryAsync_WithNoRecords_ReturnsZeroCountAndNullMostRecent()
    {
        var store = CreateStore();

        var summary = await store.GetSummaryAsync();

        summary.Count.Should().Be(0);
        summary.MostRecentGeneratedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_WithRecords_ReturnsCountAndMostRecentGeneratedAtUtc_NotTheLastInsertedRecord()
    {
        var store = CreateStore();
        var newest = CreateRecord(new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc));
        var oldest = CreateRecord(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var middle = CreateRecord(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));
        // Inserted out of chronological order: the newest record is saved first, so a naive
        // "last inserted" read would return oldest/middle's timestamp instead of newest's.
        await store.SaveAsync(newest);
        await store.SaveAsync(oldest);
        await store.SaveAsync(middle);

        var summary = await store.GetSummaryAsync();

        summary.Count.Should().Be(3);
        summary.MostRecentGeneratedAtUtc.Should().Be(newest.GeneratedAtUtc);
    }
}
