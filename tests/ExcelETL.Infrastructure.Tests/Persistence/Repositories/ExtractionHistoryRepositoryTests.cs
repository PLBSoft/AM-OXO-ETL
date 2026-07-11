using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class ExtractionHistoryRepositoryTests
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("ExtractionHistoryRepositoryTests_" + Guid.NewGuid());

    private ExtractionHistoryRepository CreateRepository() => new(_dbContextFactory);

    [Fact]
    public async Task AddAsync_PersistsHistoryEntry()
    {
        var repository = CreateRepository();
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");

        await repository.AddAsync(history);

        var result = await repository.GetByIdAsync(history.Id);
        result.Should().NotBeNull();
        result!.SourceFileName.Should().Be("invoice.xlsx");
        result.Status.Should().Be(ExtractionStatus.Pending);
    }

    [Fact]
    public async Task MarkCompletedAsync_UpdatesStatusAndStoredFilePath()
    {
        var repository = CreateRepository();
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");
        await repository.AddAsync(history);

        await repository.MarkCompletedAsync(history.Id, @"C:\archive\invoice-processed.xlsx");

        var result = await repository.GetByIdAsync(history.Id);
        result!.Status.Should().Be(ExtractionStatus.Completed);
        result.StoredFilePath.Should().Be(@"C:\archive\invoice-processed.xlsx");
    }

    [Fact]
    public async Task MarkFailedAsync_UpdatesStatusToFailed()
    {
        var repository = CreateRepository();
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");
        await repository.AddAsync(history);

        await repository.MarkFailedAsync(history.Id);

        var result = await repository.GetByIdAsync(history.Id);
        result!.Status.Should().Be(ExtractionStatus.Failed);
    }

    [Fact]
    public async Task MarkCompletedAsync_WithUnknownId_ThrowsInvalidOperationException()
    {
        var repository = CreateRepository();

        var act = async () => await repository.MarkCompletedAsync(Guid.NewGuid(), @"C:\archive\file.xlsx");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAllOrderedByDateDescendingAsync_ReturnsMostRecentFirst()
    {
        var repository = CreateRepository();
        var older = new ExtractionHistory(DateTimeOffset.UtcNow.AddHours(-1), "older.xlsx");
        var newer = new ExtractionHistory(DateTimeOffset.UtcNow, "newer.xlsx");
        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        var result = await repository.GetAllOrderedByDateDescendingAsync();

        result.Select(h => h.SourceFileName).Should().Equal("newer.xlsx", "older.xlsx");
    }

    [Fact]
    public async Task GetStatisticsAsync_WithNoEntries_ReturnsAllZeroesAndNullAverageDuration()
    {
        var repository = CreateRepository();

        var result = await repository.GetStatisticsAsync();

        result.TotalJobs.Should().Be(0);
        result.PendingJobs.Should().Be(0);
        result.CompletedJobs.Should().Be(0);
        result.FailedJobs.Should().Be(0);
        result.AverageCompletedDuration.Should().BeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_CountsJobsByStatus()
    {
        var repository = CreateRepository();

        var pending = new ExtractionHistory(DateTimeOffset.UtcNow, "pending.xlsx");
        await repository.AddAsync(pending);

        var completed = new ExtractionHistory(DateTimeOffset.UtcNow, "completed.xlsx");
        await repository.AddAsync(completed);
        await repository.MarkCompletedAsync(completed.Id, @"C:\archive\completed.xlsx");

        var failed = new ExtractionHistory(DateTimeOffset.UtcNow, "failed.xlsx");
        await repository.AddAsync(failed);
        await repository.MarkFailedAsync(failed.Id);

        var result = await repository.GetStatisticsAsync();

        result.TotalJobs.Should().Be(3);
        result.PendingJobs.Should().Be(1);
        result.CompletedJobs.Should().Be(1);
        result.FailedJobs.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_AveragesDurationAcrossCompletedJobsOnly()
    {
        var repository = CreateRepository();

        var completed = new ExtractionHistory(DateTimeOffset.UtcNow.AddMinutes(-10), "completed.xlsx");
        await repository.AddAsync(completed);
        await repository.MarkCompletedAsync(completed.Id, @"C:\archive\completed.xlsx");

        var failed = new ExtractionHistory(DateTimeOffset.UtcNow.AddHours(-1), "failed.xlsx");
        await repository.AddAsync(failed);
        await repository.MarkFailedAsync(failed.Id);

        var result = await repository.GetStatisticsAsync();

        result.AverageCompletedDuration.Should().NotBeNull();
        result.AverageCompletedDuration!.Value.Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5));
    }
}
