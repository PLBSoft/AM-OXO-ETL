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
}
