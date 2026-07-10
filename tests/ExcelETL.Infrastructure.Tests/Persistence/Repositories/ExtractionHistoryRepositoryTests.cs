using ExcelETL.Domain.Entities;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class ExtractionHistoryRepositoryTests
{
    private static ExcelEtlDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExcelEtlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AddAsync_PersistsHistoryEntryAfterSaveChanges()
    {
        await using var context = CreateContext();
        var repository = new ExtractionHistoryRepository(context);
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");

        await repository.AddAsync(history);
        await repository.SaveChangesAsync();

        context.ExtractionHistories.Should().ContainSingle(h => h.Id == history.Id);
    }
}
