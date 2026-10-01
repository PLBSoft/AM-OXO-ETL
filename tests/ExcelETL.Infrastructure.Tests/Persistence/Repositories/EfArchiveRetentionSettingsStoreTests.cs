using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Exceptions;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class EfArchiveRetentionSettingsStoreTests
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _factory =
        new TestDbContextFactory("EfArchiveRetentionSettingsStoreTests_" + Guid.NewGuid());

    private IArchiveRetentionSettingsStore CreateStore() => new EfArchiveRetentionSettingsStore(_factory);

    [Fact]
    public async Task GetRetentionDaysAsync_WhenNothingSaved_ReturnsTheDefault90()
    {
        (await CreateStore().GetRetentionDaysAsync()).Should().Be(90);
    }

    [Fact]
    public async Task SaveRetentionDaysAsync_ThenGet_ReturnsTheSavedValue_AndUpdatesInPlace()
    {
        var store = CreateStore();

        await store.SaveRetentionDaysAsync(30);
        (await store.GetRetentionDaysAsync()).Should().Be(30);

        await store.SaveRetentionDaysAsync(0);
        (await store.GetRetentionDaysAsync()).Should().Be(0);

        await using var context = await _factory.CreateDbContextAsync();
        (await context.ArchiveRetentionSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveRetentionDaysAsync_WithAnOutOfRangeValue_ThrowsAndKeepsThePreviousValue()
    {
        var store = CreateStore();
        await store.SaveRetentionDaysAsync(45);

        var act = () => store.SaveRetentionDaysAsync(-1);

        await act.Should().ThrowAsync<DomainArgumentOutOfRangeException>();
        (await store.GetRetentionDaysAsync()).Should().Be(45);
    }
}
