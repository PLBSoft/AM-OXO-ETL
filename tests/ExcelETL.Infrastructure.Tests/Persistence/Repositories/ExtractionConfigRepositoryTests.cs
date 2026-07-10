using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class ExtractionConfigRepositoryTests
{
    private static ExcelEtlDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExcelEtlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetByIdAsync_WithExistingConfig_ReturnsConfigWithSheetsAndCellMappings()
    {
        await using var context = CreateContext();

        var config = new ExtractionConfig("Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);

        context.ExtractionConfigs.Add(config);
        await context.SaveChangesAsync();

        var repository = new ExtractionConfigRepository(context);
        var result = await repository.GetByIdAsync(config.Id);

        result.Should().NotBeNull();
        result!.Sheets.Should().ContainSingle();
        result.Sheets.Single().CellMappings.Should().ContainSingle(m => m.TargetPropertyName == "SupplierName");
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new ExtractionConfigRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
