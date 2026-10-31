using System.Globalization;
using Bunit;
using ExcelETL.Application.Archiving;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.8): the plan PDFs downloadable from
// /generated-files, same lazy mechanism as the source/target files, "File expired" after the purge.
public sealed class GeneratedFilesPlansTests : BunitContext
{
    private const string Paysage = "PLANS ISOLEMENT (Paysage)";
    private const string Platinage = "PLANS PLATINAGE & JOINTS";

    private readonly Mock<IGeneratedFileArchiveStore> _archiveStore = new();
    private readonly string _archiveRoot = Path.Combine(Path.GetTempPath(), "GeneratedFilesPlansTests_" + Guid.NewGuid());
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentUICulture;

    public GeneratedFilesPlansTests()
    {
        Directory.CreateDirectory(_archiveRoot);
        Services.AddSingleton(_archiveStore.Object);
        Services.AddSingleton(Mock.Of<ILocalTimeFormatter>());
        Services.AddSingleton(Mock.Of<IArchiveRetentionSettingsStore>());
        Services.AddSingleton(Mock.Of<IGeneratedFilePurger>());
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<IOptions<GeneratedFilesArchiveOptions>>(
            Options.Create(new GeneratedFilesArchiveOptions { RootPath = _archiveRoot }));
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentUICulture = _originalCulture;
        if (disposing && Directory.Exists(_archiveRoot))
        {
            Directory.Delete(_archiveRoot, recursive: true);
        }

        base.Dispose(disposing);
    }

    private string WriteFile(byte[] content)
    {
        var relativePath = $"{Guid.NewGuid()}.bin";
        File.WriteAllBytes(Path.Combine(_archiveRoot, relativePath), content);
        return relativePath;
    }

    private GeneratedFileRecord Record(IReadOnlyList<GeneratedFilePlan> plans, bool purged = false)
    {
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, "38-G6306B", "source.xlsx", WriteFile([1]), "target.xlsx", WriteFile([2]),
            Guid.NewGuid(), Guid.NewGuid(), GeneratedFileArchiveStatus.Success, plans: plans);
        if (purged)
        {
            record.MarkFilesPurged(DateTime.UtcNow);
        }

        _archiveStore.Setup(s => s.SearchAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GeneratedFileRecord>)[record]);
        return record;
    }

    private GeneratedFilePlan Plan(string sheet, int page, byte[] content) =>
        new(sheet, page, $"MAD_38-G6306B_{sheet}_p{page}.pdf", WriteFile(content), content.Length);

    [Fact]
    public void RecordWithoutPlans_ShowsADash()
    {
        var record = Record([]);

        var cut = Render<GeneratedFiles>();

        cut.Find($"#plans-none-{record.Id}").TextContent.Should().Be("—");
    }

    [Fact]
    public void Plans_AreCollapsedBehindTheirCount_ThenListedInOrder()
    {
        var record = Record([Plan(Platinage, 1, [9]), Plan(Paysage, 1, [8])]);

        var cut = Render<GeneratedFiles>();

        cut.Find($"#plans-toggle-{record.Id}").TextContent.Trim().Should().Be("2");
        cut.FindAll($"#plans-list-{record.Id}").Should().BeEmpty();

        cut.Find($"#plans-toggle-{record.Id}").Click();

        cut.FindAll($"#plans-list-{record.Id} li").Select(li => li.TextContent.Trim()).Should().Equal(
            $"{Paysage} - page 1", $"{Platinage} - page 1");
    }

    [Fact]
    public void PreparingAPlanDownload_GivesAPdfLinkWithItsFileName()
    {
        var record = Record([Plan(Paysage, 1, [1, 2, 3])]);
        var cut = Render<GeneratedFiles>();
        cut.Find($"#plans-toggle-{record.Id}").Click();

        cut.Find($"#prepare-download-plan-button-{record.Id}-0").Click();

        var link = cut.Find($"#download-plan-link-{record.Id}-0");
        link.GetAttribute("href").Should().Be("data:application/pdf;base64," + Convert.ToBase64String([1, 2, 3]));
        link.GetAttribute("download").Should().Be($"MAD_38-G6306B_{Paysage}_p1.pdf");
    }

    [Fact]
    public void PurgedRecord_ShowsFileExpired_InsteadOfThePlans()
    {
        var record = Record([Plan(Paysage, 1, [1])], purged: true);

        var cut = Render<GeneratedFiles>();

        cut.Find($"#plans-expired-{record.Id}").TextContent.Should().Be("File expired");
        cut.FindAll($"#plans-toggle-{record.Id}").Should().BeEmpty();
    }

    [Fact]
    public void MobileCard_HasItsOwnPlansCell()
    {
        var record = Record([Plan(Paysage, 1, [1])]);

        var cut = Render<GeneratedFiles>();

        cut.Find($"#plans-toggle-card-{record.Id}").Click();
        cut.FindAll($"#prepare-download-plan-button-card-{record.Id}-0").Should().ContainSingle();
    }
}
