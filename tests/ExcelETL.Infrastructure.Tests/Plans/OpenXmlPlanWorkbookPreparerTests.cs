using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using ExcelETL.Infrastructure.Plans;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.4): plan sheets prepared for printing,
// on the real client files. Page counts are those of the LibreOffice proof of concept (12-13/10), whose
// PDFs were checked by Simon against an Excel export.
public class OpenXmlPlanWorkbookPreparerTests
{
    private const string Portrait = "PLANS ISOLEMENT (Portrait)";
    private const string Paysage = "PLANS ISOLEMENT (Paysage)";
    private const string Platinage = "PLANS PLATINAGE & JOINTS";

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static TheoryData<string, int, int, int> ExpectedPageCounts() => new()
    {
        { "Dossier.de.MaD.IDL.-.C7401.xlsx", 0, 1, 0 },
        { "Dossier de MaD IDL -  C8503 PORTE FILTRE.xlsx", 0, 1, 1 },
        { "Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx", 4, 7, 4 },
        { "Dossier de MaD IDL -  E3201B condensats.xlsx", 2, 0, 1 },
        { "Dossier de MaD IDL -  E6423 Repose cellule.xlsx", 0, 2, 1 },
        { "Dossier de MaD IDL -  E6431A Dépose cellule.xlsx", 0, 2, 2 },
        { "Dossier de MaD IDL -  E8201B Echange standard.xlsx", 0, 1, 1 },
        { "Dossier de MaD IDL -  E8582.xlsx", 2, 0, 1 },
        { "Dossier de MaD IDL -  G4010A.xlsx", 0, 1, 1 },
        { "Dossier.de.MaD.IDL.-.G6306B.REV.xlsx", 0, 1, 2 },
        { "Dossier de MaD IDL -  LRS4504.xlsx", 1, 0, 0 },
        { "Dossier de MaD IDL -  PSA BOUTEILLE B  XCV7552 B.xlsx", 0, 1, 1 },
        { "Dossier de MaD IDL -  PSA BOUTEILLE E XCV7552 E.xlsx", 0, 1, 1 },
        { "Dossier de MaD IDL -  RANGEE N°1.xlsx", 1, 1, 2 },
    };

    [Theory]
    [MemberData(nameof(ExpectedPageCounts))]
    public void Prepare_RealFiles_FindsTheSameNonEmptyPagesAsTheProofOfConcept(
        string fixture, int portraitPages, int paysagePages, int platinagePages)
    {
        var workbook = File.ReadAllBytes(FixturePath(fixture));

        PageCount(workbook, Portrait).Should().Be(portraitPages);
        PageCount(workbook, Paysage).Should().Be(paysagePages);
        PageCount(workbook, Platinage).Should().Be(platinagePages);
    }

    [Fact]
    public void Prepare_SheetNameWithATrailingSpace_IsFound_AndItsWidthCoversTheRightMostBubble()
    {
        var workbook = File.ReadAllBytes(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));

        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, " plans isolement (paysage)");

        prepared.Should().NotBeNull();
        prepared!.SheetName.Should().Be("PLANS ISOLEMENT (Paysage) ");
        var sheet = SheetLayout(workbook, prepared.SheetName);
        prepared.Layout.LastColumn.Should().BeGreaterThan(sheet.HeaderLastColumn);
    }

    [Fact]
    public void Prepare_MissingSheet_ReturnsNull()
    {
        var workbook = File.ReadAllBytes(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));

        OpenXmlPlanWorkbookPreparer.Prepare(workbook, Platinage).Should().BeNull();
    }

    [Fact]
    public void Prepare_SheetWithoutAnyPlan_ReturnsNoWorkbookToPrint()
    {
        var workbook = File.ReadAllBytes(FixturePath("Dossier de MaD IDL -  LRS4504.xlsx"));

        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, Platinage);

        prepared!.Layout.Pages.Should().BeEmpty();
        prepared.PreparedWorkbook.Should().BeNull();
    }

    [Fact]
    public void Prepare_G6306BPlatinage_SkipsTheEmptyFirstPage_AndSetsThePrintArea()
    {
        var workbook = File.ReadAllBytes(FixturePath("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"));

        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, Platinage)!;

        prepared.Layout.Pages.Should().HaveCount(2);
        prepared.Layout.Pages[0].FirstRow.Should().BeGreaterThan(4, "the first page of this sheet is empty");

        var copy = ReadWorkbookXml(prepared.PreparedWorkbook!);
        var sheets = copy.Root!.Element(S + "sheets")!.Elements(S + "sheet").ToList();
        var index = sheets.FindIndex(s => (string)s.Attribute("name")! == prepared.SheetName);
        sheets.Where((_, i) => i != index).Should().OnlyContain(s => (string?)s.Attribute("state") == "hidden");
        sheets[index].Attribute("state").Should().BeNull();

        var printArea = copy.Root.Element(S + "definedNames")!.Elements(S + "definedName")
            .Single(d => (string)d.Attribute("name")! == "_xlnm.Print_Area");
        ((string)printArea.Attribute("localSheetId")!).Should().Be(index.ToString());
        printArea.Value.Split(',').Should().HaveCount(2)
            .And.AllSatisfy(range => range.Should().StartWith("'PLANS PLATINAGE & JOINTS'!$A$"));
    }

    [Fact]
    public void Prepare_SetsFitToOnePagePerRange_AndRemovesManualBreaks()
    {
        var workbook = File.ReadAllBytes(FixturePath("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, Paysage)!;

        var sheet = ReadSheetXml(prepared.PreparedWorkbook!, prepared.SheetName);
        sheet.Root!.Element(S + "sheetPr")!.Element(S + "pageSetUpPr")!.Attribute("fitToPage")!.Value.Should().Be("1");
        var pageSetup = sheet.Root.Element(S + "pageSetup")!;
        pageSetup.Attribute("fitToWidth")!.Value.Should().Be("1");
        pageSetup.Attribute("fitToHeight")!.Value.Should().Be("1");
        pageSetup.Attribute("scale").Should().BeNull();
        sheet.Root.Elements(S + "rowBreaks").Should().BeEmpty();
    }

    // Only workbook.xml and the printed sheet are rewritten: drawings and images are untouched, so no
    // shape can be lost on the way.
    [Theory]
    [InlineData("Dossier de MaD IDL -  PSA BOUTEILLE B  XCV7552 B.xlsx", Paysage)]
    [InlineData("Dossier de MaD IDL -  E6431A Dépose cellule.xlsx", Platinage)]
    [InlineData("Dossier de MaD IDL -  RANGEE N°1.xlsx", Platinage)]
    public void Prepare_LeavesEveryOtherPartByteForByte(string fixture, string sheetName)
    {
        var workbook = File.ReadAllBytes(FixturePath(fixture));

        var prepared = OpenXmlPlanWorkbookPreparer.Prepare(workbook, sheetName)!;

        var before = PartHashes(workbook);
        var after = PartHashes(prepared.PreparedWorkbook!);
        var sheetPart = SheetPartName(workbook, prepared.SheetName);
        var changed = before.Keys.Where(name => !after.TryGetValue(name, out var hash) || hash != before[name]).ToList();

        changed.Should().BeEquivalentTo(["xl/workbook.xml", sheetPart]);
        after.Keys.Should().BeEquivalentTo(before.Keys);
        before.Keys.Should().Contain(name => name.StartsWith("xl/drawings/drawing"));
    }

    private static int PageCount(byte[] workbook, string sheetName) =>
        OpenXmlPlanWorkbookPreparer.Prepare(workbook, sheetName)?.Layout.Pages.Count ?? 0;

    private static (int HeaderLastColumn, int Unused) SheetLayout(byte[] workbook, string sheetName)
    {
        var sheet = ReadSheetXml(workbook, sheetName);
        var headerLastColumn = sheet.Root!.Element(S + "mergeCells")!.Elements(S + "mergeCell")
            .Select(m => ((string)m.Attribute("ref")!).Split(':'))
            .Where(parts => int.Parse(new string(parts[0].SkipWhile(char.IsLetter).ToArray())) <= 4)
            .Max(parts => new string(parts[1].TakeWhile(char.IsLetter).ToArray()).Aggregate(0, (n, c) => n * 26 + (c - 'A' + 1)));
        return (headerLastColumn, 0);
    }

    private static Dictionary<string, string> PartHashes(byte[] workbook)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        return zip.Entries.Where(e => !e.FullName.EndsWith('/')).ToDictionary(
            e => e.FullName,
            e =>
            {
                using var stream = e.Open();
                return Convert.ToHexString(SHA256.HashData(stream));
            });
    }

    private static XDocument ReadWorkbookXml(byte[] workbook) => ReadEntry(workbook, "xl/workbook.xml");

    private static XDocument ReadSheetXml(byte[] workbook, string sheetName) =>
        ReadEntry(workbook, SheetPartName(workbook, sheetName));

    private static string SheetPartName(byte[] workbook, string sheetName)
    {
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace rels = "http://schemas.openxmlformats.org/package/2006/relationships";
        var id = ReadWorkbookXml(workbook).Root!.Element(S + "sheets")!.Elements(S + "sheet")
            .Single(s => (string)s.Attribute("name")! == sheetName).Attribute(r + "id")!.Value;
        var target = (string)ReadEntry(workbook, "xl/_rels/workbook.xml.rels").Root!.Elements(rels + "Relationship")
            .Single(e => (string)e.Attribute("Id")! == id).Attribute("Target")!;
        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static XDocument ReadEntry(byte[] workbook, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        using var stream = zip.GetEntry(name)!.Open();
        return XDocument.Load(stream);
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
