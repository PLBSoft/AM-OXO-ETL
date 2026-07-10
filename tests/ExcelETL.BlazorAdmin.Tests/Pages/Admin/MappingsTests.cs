using Bunit;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class MappingsTests : BunitContext
{
    public MappingsTests()
    {
        Services.AddDbContextFactory<ExcelEtlDbContext>(options =>
            options.UseInMemoryDatabase("MappingsTests_" + Guid.NewGuid()));
    }

    private async Task SeedConfigAsync(ExtractionConfig config)
    {
        var factory = Services.GetRequiredService<IDbContextFactory<ExcelEtlDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        context.ExtractionConfigs.Add(config);
        await context.SaveChangesAsync();
    }

    private static ExtractionConfig BuildConfigWithOneSheetAndMapping()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);
        return config;
    }

    [Fact]
    public async Task Mappings_WithExistingConfig_DisplaysConfigNameAndSheetCount()
    {
        await SeedConfigAsync(BuildConfigWithOneSheetAndMapping());

        var cut = Render<Mappings>();

        cut.Markup.Should().Contain("Purchase Order Template");
        cut.Markup.Should().Contain("1 sheet(s)");
    }

    [Fact]
    public void CreateConfig_WithValidName_AddsNewConfigToList()
    {
        var cut = Render<Mappings>();

        cut.Find("#new-config-name-input").Change("New Template");
        cut.Find("#create-config-button").Click();

        cut.Markup.Should().Contain("New Template");
    }

    [Fact]
    public async Task SelectConfig_DisplaysItsSheetsAndCellMappings()
    {
        await SeedConfigAsync(BuildConfigWithOneSheetAndMapping());
        var cut = Render<Mappings>();

        cut.Find("li.list-group-item").Click();

        cut.Markup.Should().Contain("Summary");
        cut.Markup.Should().Contain("SupplierName");
        cut.Markup.Should().Contain("B2");
    }

    [Fact]
    public async Task AddSheet_BeyondFiveSheets_DisplaysDomainValidationError()
    {
        var config = new ExtractionConfig("Full Template");
        for (var i = 0; i < 5; i++)
        {
            config.AddSheet(new SheetConfig($"Sheet{i}", sheetIndex: i));
        }
        await SeedConfigAsync(config);

        var cut = Render<Mappings>();
        cut.Find("li.list-group-item").Click();

        cut.Find("#new-sheet-name-input").Change("SixthSheet");
        cut.Find("#new-sheet-index-input").Change("5");
        cut.Find("#add-sheet-button").Click();

        cut.Markup.Should().Contain("cannot add more than 5");
    }

    [Fact]
    public async Task AddCellMapping_WithInvalidSourceCell_DisplaysDomainValidationError()
    {
        var config = new ExtractionConfig("Purchase Order Template");
        config.AddSheet(new SheetConfig("Summary", sheetIndex: 0));
        await SeedConfigAsync(config);

        var cut = Render<Mappings>();
        cut.Find("li.list-group-item").Click();

        cut.Find(".mapping-source-cell-input").Change("not-a-cell");
        cut.Find(".mapping-target-property-input").Change("SupplierName");
        cut.Find(".add-mapping-button").Click();

        cut.Markup.Should().Contain("valid Excel cell reference");
    }
}
