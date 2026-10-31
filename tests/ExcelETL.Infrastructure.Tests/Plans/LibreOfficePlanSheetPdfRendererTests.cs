using ExcelETL.Infrastructure.Plans;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.5). Failure paths use a fake soffice
// (a .cmd script); the real-rendering tests need LibreOffice installed and are tagged "LibreOffice".
public sealed class LibreOfficePlanSheetPdfRendererTests : IDisposable
{
    private const string Paysage = "PLANS ISOLEMENT (Paysage)";
    private const string Platinage = "PLANS PLATINAGE & JOINTS";
    private const string Portrait = "PLANS ISOLEMENT (Portrait)";

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "LibreOfficePlanSheetPdfRendererTests_" + Guid.NewGuid().ToString("N"));

    public LibreOfficePlanSheetPdfRendererTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { }
    }

    private static LibreOfficePlanSheetPdfRenderer CreateRenderer(
        string sofficePath, int timeoutSeconds = 60, string? profileDirectory = null) =>
        new(Options.Create(new PlanRenderingOptions
            {
                SofficePath = sofficePath, TimeoutSeconds = timeoutSeconds, ProfileDirectory = profileDirectory,
            }),
            NullLogger<LibreOfficePlanSheetPdfRenderer>.Instance);

    private string FakeSoffice(string body)
    {
        var path = Path.Combine(_scratch, $"fake-soffice-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(path, "@echo off\r\n" + body + "\r\n");
        return path;
    }

    private static byte[] Fixture(string fileName) => File.ReadAllBytes(FixturePath(fileName));

    [Fact]
    public async Task MissingSheet_ReturnsNull_WithoutLaunchingLibreOffice()
    {
        var renderer = CreateRenderer(Path.Combine(_scratch, "not-installed.exe"));

        var result = await renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.C7401.xlsx"), Platinage);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SheetWithoutPlan_ReturnsNoPage_WithoutLaunchingLibreOffice()
    {
        var renderer = CreateRenderer(Path.Combine(_scratch, "not-installed.exe"));

        var result = await renderer.RenderAsync(Fixture("Dossier de MaD IDL -  LRS4504.xlsx"), Platinage);

        result!.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task LibreOfficeNotInstalled_ThrowsAClearError()
    {
        var renderer = CreateRenderer(Path.Combine(_scratch, "not-installed.exe"));

        var act = () => renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"), Platinage);

        (await act.Should().ThrowAsync<PlanRenderingException>()).Which.Message.Should().Contain("not-installed.exe");
    }

    [Fact]
    public async Task ProcessFailing_ThrowsWithTheExitCode()
    {
        var renderer = CreateRenderer(FakeSoffice("echo conversion impossible 1>&2\r\nexit /b 3"));

        var act = () => renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"), Platinage);

        (await act.Should().ThrowAsync<PlanRenderingException>()).Which.Message.Should().Contain("exit code 3");
    }

    [Fact]
    public async Task ProcessEndingWithoutPdf_Throws()
    {
        var renderer = CreateRenderer(FakeSoffice("exit /b 0"));

        var act = () => renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"), Platinage);

        (await act.Should().ThrowAsync<PlanRenderingException>()).Which.Message.Should().Contain("without producing a PDF");
    }

    [Fact]
    public async Task ProcessTooSlow_IsKilled_AndThrows()
    {
        var renderer = CreateRenderer(FakeSoffice("ping -n 30 127.0.0.1 > nul"), timeoutSeconds: 1);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var act = () => renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"), Platinage);

        (await act.Should().ThrowAsync<PlanRenderingException>()).Which.Message.Should().Contain("within 1 s");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    // Another process converting with the same LibreOffice profile holds the lock file: wait, then give up.
    [Fact]
    public async Task ProfileLockedByAnotherProcess_WaitsThenThrows()
    {
        var profileDirectory = Path.Combine(_scratch, "profile");
        var renderer = CreateRenderer(FakeSoffice("exit /b 0"), timeoutSeconds: 1, profileDirectory: profileDirectory);
        await using var otherProcessLock = new FileStream(
            profileDirectory + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var act = () => renderer.RenderAsync(Fixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"), Platinage);

        (await act.Should().ThrowAsync<PlanRenderingException>()).Which.Message.Should().Contain("busy");
    }

    [Theory]
    [Trait("Category", "LibreOffice")]
    [InlineData("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx", Platinage, 2)]
    [InlineData("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx", Paysage, 7)]
    [InlineData("Dossier de MaD IDL -  LRS4504.xlsx", Portrait, 1)]
    public async Task RealLibreOffice_PrintsOneSinglePagePdfPerNonEmptyPage(string fixture, string sheetName, int expectedPdfs)
    {
        var renderer = CreateRenderer(PlanRenderingOptions.DefaultSofficePath);

        var result = await renderer.RenderAsync(Fixture(fixture), sheetName);

        result!.Pages.Should().HaveCount(expectedPdfs);
        foreach (var pdf in result.Pages)
        {
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            document.PageCount.Should().Be(1);
        }
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the tests/Fixtures directory.");
        }

        return Path.Combine(directory.FullName, "Fixtures", fileName);
    }
}
