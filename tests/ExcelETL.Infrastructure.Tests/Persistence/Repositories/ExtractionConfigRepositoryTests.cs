using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
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
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("ExtractionConfigRepositoryTests_" + Guid.NewGuid());

    private ExtractionConfigRepository CreateRepository() => new(_dbContextFactory);

    [Fact]
    public async Task GetByIdAsync_WithExistingConfig_ReturnsConfigWithSheetsAndCellMappings()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);

        var repository = CreateRepository();
        await repository.AddAsync(config);

        var result = await repository.GetByIdAsync(config.Id);

        result.Should().NotBeNull();
        result!.Sheets.Should().ContainSingle();
        result.Sheets.Single().CellMappings.Should().ContainSingle(m => m.TargetPropertyName == "SupplierName");
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var repository = CreateRepository();

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigsOrderedByName()
    {
        var repository = CreateRepository();
        await repository.AddAsync(new ExtractionConfig("Zebra Template"));
        await repository.AddAsync(new ExtractionConfig("Alpha Template"));

        var result = await repository.GetAllAsync();

        result.Select(c => c.Name).Should().Equal("Alpha Template", "Zebra Template");
    }

    [Fact]
    public async Task AddSheetAsync_WithValidSheet_PersistsSheetOnConfig()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        var repository = CreateRepository();
        await repository.AddAsync(config);

        await repository.AddSheetAsync(config.Id, new SheetConfig("Summary", sheetIndex: 0));

        var reloaded = await repository.GetByIdAsync(config.Id);
        reloaded!.Sheets.Should().ContainSingle(s => s.SheetName == "Summary");
    }

    [Fact]
    public async Task AddSheetAsync_BeyondFiveSheets_ThrowsAndDoesNotPersist()
    {
        var config = new ExtractionConfig("Full Template");
        for (var i = 0; i < 5; i++)
        {
            config.AddSheet(new SheetConfig($"Sheet{i}", sheetIndex: i));
        }
        var repository = CreateRepository();
        await repository.AddAsync(config);

        var act = async () => await repository.AddSheetAsync(config.Id, new SheetConfig("SixthSheet", sheetIndex: 5));

        await act.Should().ThrowAsync<InvalidOperationException>();
        var reloaded = await repository.GetByIdAsync(config.Id);
        reloaded!.Sheets.Should().HaveCount(5);
    }

    [Fact]
    public async Task AddSheetAsync_WithUnknownConfigId_ThrowsExtractionConfigNotFoundException()
    {
        var repository = CreateRepository();

        var act = async () => await repository.AddSheetAsync(Guid.NewGuid(), new SheetConfig("Summary", sheetIndex: 0));

        await act.Should().ThrowAsync<ExtractionConfigNotFoundException>();
    }

    [Fact]
    public async Task AddCellMappingAsync_WithValidMapping_PersistsMappingOnSheet()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        config.AddSheet(sheet);
        var repository = CreateRepository();
        await repository.AddAsync(config);

        await repository.AddCellMappingAsync(
            config.Id, sheet.Id, new CellMapping("B2", "SupplierName", CellDataType.Text));

        var reloaded = await repository.GetByIdAsync(config.Id);
        reloaded!.Sheets.Single().CellMappings.Should().ContainSingle(m => m.TargetPropertyName == "SupplierName");
    }

    [Fact]
    public async Task AddCellMappingAsync_WithUnknownConfigId_ThrowsExtractionConfigNotFoundException()
    {
        var repository = CreateRepository();

        var act = async () => await repository.AddCellMappingAsync(
            Guid.NewGuid(), Guid.NewGuid(), new CellMapping("B2", "SupplierName", CellDataType.Text));

        await act.Should().ThrowAsync<ExtractionConfigNotFoundException>();
    }

    [Fact]
    public async Task AddCellMappingAsync_WithUnknownSheetId_ThrowsSheetNotFoundInExtractionConfigException()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        config.AddSheet(new SheetConfig("Summary", sheetIndex: 0));
        var repository = CreateRepository();
        await repository.AddAsync(config);
        var unknownSheetId = Guid.NewGuid();

        var act = async () => await repository.AddCellMappingAsync(
            config.Id, unknownSheetId, new CellMapping("B2", "SupplierName", CellDataType.Text));

        (await act.Should().ThrowAsync<SheetNotFoundInExtractionConfigException>())
            .Which.Should().Match<SheetNotFoundInExtractionConfigException>(ex =>
                ex.ExtractionConfigId == config.Id
                && ex.SheetId == unknownSheetId
                && ex.ErrorCode == ApplicationErrorCode.SheetNotFoundInExtractionConfig);
    }
}
