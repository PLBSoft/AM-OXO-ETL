using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.2 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportColumnLayoutTests
{
    [Fact]
    public void For_EquipementRule_OrdersDescriptiveThenConstantThenApplicationThenPointColumns()
    {
        var descriptive = new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere);
        var empty = new ColumnDefinition("LOC2", null);
        var constant = new ConstantColumnDefinition("SUPPRESSION", "N");
        var application = new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O");
        var point = new PointColumnDefinition("PROLOCK VANNES", "PROLOCK VANNES");
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, [descriptive, empty], [point], [application], [constant]);

        ExportColumnLayout.For(rule).Should().Equal(
            new ExportColumn("A", "Repère", ExportColumnKind.Descriptive, descriptive),
            new ExportColumn("B", "LOC2", ExportColumnKind.Descriptive, empty),
            new ExportColumn("C", "SUPPRESSION", ExportColumnKind.Constant, constant),
            new ExportColumn("D", "PROGRESS", ExportColumnKind.Application, application),
            new ExportColumn("E", "PROLOCK VANNES", ExportColumnKind.Point, point));
    }

    [Fact]
    public void For_LettersGoBeyondZ()
    {
        var columns = Enumerable.Range(1, 28).Select(i => new ColumnDefinition($"C{i}", null)).ToList();
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, columns, [], []);

        ExportColumnLayout.For(rule).Select(c => c.Letter).TakeLast(3).Should().Equal("Z", "AA", "AB");
    }

    [Fact]
    public void For_TacheMultipleRule_HasDescriptiveThenConstantColumns()
    {
        var ordre = new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre);
        var constant = new ConstantColumnDefinition("SUPPRESSION", "N");
        var rule = new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, [ordre], [], [], [constant]);

        ExportColumnLayout.For(rule).Select(c => (c.Letter, c.Kind)).Should().Equal(
            ("A", ExportColumnKind.Descriptive), ("B", ExportColumnKind.Constant));
    }

    // Guard against drift with the real engine: the page's column letters are only right if this order is
    // the one SheetGenerationEngine writes.
    [Fact]
    public void For_EverySeededRule_MatchesTheHeadersTheRealEngineWrites()
    {
        var profile = SeededExportProfile();
        var equipement = new EquipementPivot("38-C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX", "PROCEDURE");
        var isolement = new IsolementPivot("C7401-V1", "Vanne", "PROLOCK", "O", "", sourceSheetName: "ISOLEMENT");
        var tache = new TacheMultiplePivot(1, "Action", "", "", "TM_PROC_MAD", null, estFactice: false, ligneSource: 9);
        var importResult = new ImportResult(equipement, [isolement], [], [tache], []);

        var workbook = new SheetGenerationEngine(NullLogger<SheetGenerationEngine>.Instance).Generate(importResult, profile);

        profile.SheetRules.Should().HaveCount(3);
        foreach (var rule in profile.SheetRules)
        {
            var sheetName = rule.PivotSource == PivotSource.TacheMultiple ? "TM_PROC_MAD" : rule.SheetName;
            var generated = workbook.Sheets.Single(s => s.Name == sheetName);
            ExportColumnLayout.For(rule).Select(c => c.Header).Should().Equal(generated.Headers, $"rule {rule.SheetName}");
        }
    }

    private static ExportProfile SeededExportProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ExportColumnLayoutTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId).GetAwaiter().GetResult()!;
    }
}
