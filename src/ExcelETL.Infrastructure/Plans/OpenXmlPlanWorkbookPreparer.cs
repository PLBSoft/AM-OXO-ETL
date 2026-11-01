using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using ExcelETL.Application.Plans;

namespace ExcelETL.Infrastructure.Plans;

// The result of preparing one plan sheet for printing. PreparedWorkbook is null when the sheet has no
// page to print (nothing to hand to the renderer).
public sealed record PreparedPlanSheet(string SheetName, PlanSheetPrintLayout Layout, byte[]? PreparedWorkbook);

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.4, decision D8): reads a plan
// sheet's drawings into a PlanSheetLayout, asks PlanPageDetector for the pages, then writes a copy of the
// workbook ready to print: only that sheet visible and active, print area = one range per page, "fit to
// 1 page wide x 1 tall" (applied per range, so one client page gives one PDF page), manual page breaks
// removed.
//
// The OpenXML SDK is only used to navigate the package. Parts are read and rewritten as raw XML, and only
// workbook.xml and the target sheet are rewritten: drawings, images and every other part are left byte for
// byte as they were (never re-saved through ClosedXML, which could lose shapes).
public static class OpenXmlPlanWorkbookPreparer
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private static readonly Regex CellReference = new("^\\$?([A-Z]+)\\$?(\\d+)$", RegexOptions.Compiled);

    // Returns null when the workbook has no sheet of that name (compared ignoring case and leading or
    // trailing spaces: C7401 names it "PLANS ISOLEMENT (Paysage) ").
    public static PreparedPlanSheet? Prepare(byte[] workbook, string planSheetName)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(planSheetName);

        using var stream = new MemoryStream();
        stream.Write(workbook);
        stream.Position = 0;

        string sheetName;
        PlanSheetPrintLayout layout;
        using (var document = SpreadsheetDocument.Open(stream, isEditable: true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("The workbook has no workbook part.");
            var workbookXml = Load(workbookPart);
            var sheets = workbookXml.Root!.Element(S + "sheets")?.Elements(S + "sheet").ToList() ?? [];

            var index = sheets.FindIndex(s => string.Equals(
                ((string?)s.Attribute("name"))?.Trim(), planSheetName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return null;
            }

            sheetName = (string)sheets[index].Attribute("name")!;
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById((string)sheets[index].Attribute(R + "id")!);
            var worksheetXml = Load(worksheetPart);

            layout = PlanPageDetector.Detect(ReadLayout(worksheetXml, worksheetPart));
            if (layout.Pages.Count == 0)
            {
                return new PreparedPlanSheet(sheetName, layout, null);
            }

            ShowOnly(workbookXml, sheets, index);
            SetPrintArea(workbookXml, index, sheetName, layout);
            Save(workbookPart, workbookXml);

            SetPageSetup(worksheetXml);
            Save(worksheetPart, worksheetXml);
        }

        // The package is written back to the stream when the document is disposed.
        return new PreparedPlanSheet(sheetName, layout, stream.ToArray());
    }

    // ------------------------------------------------------------------------------------------
    // Reading the layout.
    // ------------------------------------------------------------------------------------------

    internal static PlanSheetLayout ReadLayout(XDocument worksheetXml, WorksheetPart worksheetPart)
    {
        var root = worksheetXml.Root!;

        var (lastUsedColumn, lastUsedRow) = (0, 0);
        var dimension = (string?)root.Element(S + "dimension")?.Attribute("ref");
        if (dimension is not null && ParseCell(dimension.Split(':').Last()) is { } end)
        {
            (lastUsedColumn, lastUsedRow) = end;
        }

        var headerLastColumn = 0;
        foreach (var mergeCell in root.Element(S + "mergeCells")?.Elements(S + "mergeCell") ?? [])
        {
            var parts = ((string?)mergeCell.Attribute("ref"))?.Split(':');
            if (parts is { Length: 2 } && ParseCell(parts[0]) is { } start && ParseCell(parts[1]) is { } stop
                && start.Row <= PlanPageDetector.HeaderRowCount)
            {
                headerLastColumn = Math.Max(headerLastColumn, stop.Column);
            }
        }

        return new PlanSheetLayout(ReadAnchors(worksheetPart), headerLastColumn, lastUsedRow, lastUsedColumn);
    }

    private static List<PlanSheetAnchor> ReadAnchors(WorksheetPart worksheetPart)
    {
        var anchors = new List<PlanSheetAnchor>();
        var drawingsPart = worksheetPart.DrawingsPart;
        if (drawingsPart is null)
        {
            return anchors;
        }

        var drawingXml = Load(drawingsPart);
        var imageKeys = new Dictionary<Uri, string>();

        // Newer Excel versions wrap some anchors in mc:AlternateContent; the mc:Fallback copy is skipped so
        // a drawing is never counted twice.
        var anchorElements = drawingXml.Root!.Descendants()
            .Where(e => (e.Name == Xdr + "twoCellAnchor" || e.Name == Xdr + "oneCellAnchor")
                        && !e.Ancestors(Mc + "Fallback").Any());

        foreach (var anchor in anchorElements)
        {
            var from = anchor.Element(Xdr + "from");
            if (from is null)
            {
                continue;
            }

            var fromRow = ReadIndex(from, "row") + 1;
            var to = anchor.Element(Xdr + "to") ?? from;
            var toRow = ReadIndex(to, "row") + 1;
            var toColumn = ReadIndex(to, "col") + 1;

            foreach (var picture in anchor.Descendants(Xdr + "pic"))
            {
                var embed = (string?)picture.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
                anchors.Add(new PlanSheetAnchor(
                    PlanAnchorKind.Picture, fromRow, toRow, toColumn, ImageKey(drawingsPart, embed, imageKeys)));
            }

            if (anchor.Descendants().Any(e => e.Name == Xdr + "sp" || e.Name == Xdr + "cxnSp" || e.Name == Xdr + "graphicFrame"))
            {
                anchors.Add(new PlanSheetAnchor(PlanAnchorKind.Shape, fromRow, toRow, toColumn));
            }
        }

        return anchors;
    }

    // Same image content => same key, whatever the relationship or media part it goes through.
    private static string? ImageKey(DrawingsPart drawingsPart, string? relationshipId, Dictionary<Uri, string> cache)
    {
        if (relationshipId is null || drawingsPart.GetPartById(relationshipId) is not { } part)
        {
            return null;
        }

        if (!cache.TryGetValue(part.Uri, out var key))
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            key = Convert.ToHexString(SHA256.HashData(stream));
            cache[part.Uri] = key;
        }

        return key;
    }

    private static int ReadIndex(XElement marker, string name) =>
        int.Parse(marker.Element(Xdr + name)?.Value ?? "0", CultureInfo.InvariantCulture);

    private static (int Column, int Row)? ParseCell(string reference)
    {
        var match = CellReference.Match(reference.Trim());
        if (!match.Success)
        {
            return null;
        }

        return (ColumnNumber(match.Groups[1].Value), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static int ColumnNumber(string letters) => letters.Aggregate(0, (n, c) => n * 26 + (c - 'A' + 1));

    internal static string ColumnLetters(int number)
    {
        var letters = string.Empty;
        while (number > 0)
        {
            var remainder = (number - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            number = (number - 1) / 26;
        }

        return letters;
    }

    // ------------------------------------------------------------------------------------------
    // Writing the copy.
    // ------------------------------------------------------------------------------------------

    private static void ShowOnly(XDocument workbookXml, List<XElement> sheets, int index)
    {
        for (var i = 0; i < sheets.Count; i++)
        {
            if (i == index)
            {
                sheets[i].Attribute("state")?.Remove();
            }
            else
            {
                sheets[i].SetAttributeValue("state", "hidden");
            }
        }

        var position = index.ToString(CultureInfo.InvariantCulture);
        foreach (var view in workbookXml.Root!.Element(S + "bookViews")?.Elements(S + "workbookView") ?? [])
        {
            view.SetAttributeValue("activeTab", position);
            view.SetAttributeValue("firstSheet", position);
        }
    }

    private static void SetPrintArea(XDocument workbookXml, int index, string sheetName, PlanSheetPrintLayout layout)
    {
        var root = workbookXml.Root!;
        var definedNames = root.Element(S + "definedNames");
        definedNames?.Elements(S + "definedName")
            .Where(d => (string?)d.Attribute("name") is "_xlnm.Print_Area" or "_xlnm.Print_Titles")
            .Remove();

        if (definedNames is null)
        {
            definedNames = new XElement(S + "definedNames");
            // Schema order: sheets, functionGroups, externalReferences, definedNames.
            var after = root.Element(S + "externalReferences") ?? root.Element(S + "functionGroups") ?? root.Element(S + "sheets")!;
            after.AddAfterSelf(definedNames);
        }

        var quotedName = "'" + sheetName.Replace("'", "''", StringComparison.Ordinal) + "'";
        var lastColumn = ColumnLetters(layout.LastColumn);
        var ranges = layout.Pages.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{quotedName}!$A${p.FirstRow}:${lastColumn}${p.LastRow}"));

        definedNames.Add(new XElement(
            S + "definedName",
            new XAttribute("name", "_xlnm.Print_Area"),
            new XAttribute("localSheetId", index.ToString(CultureInfo.InvariantCulture)),
            string.Join(",", ranges)));
    }

    private static void SetPageSetup(XDocument worksheetXml)
    {
        var root = worksheetXml.Root!;

        foreach (var view in root.Element(S + "sheetViews")?.Elements(S + "sheetView") ?? [])
        {
            view.SetAttributeValue("tabSelected", "1");
        }

        var sheetPr = root.Element(S + "sheetPr");
        if (sheetPr is null)
        {
            sheetPr = new XElement(S + "sheetPr");
            root.AddFirst(sheetPr);
        }

        var pageSetUpPr = sheetPr.Element(S + "pageSetUpPr");
        if (pageSetUpPr is null)
        {
            // Last child of sheetPr in the schema.
            pageSetUpPr = new XElement(S + "pageSetUpPr");
            sheetPr.Add(pageSetUpPr);
        }

        pageSetUpPr.SetAttributeValue("fitToPage", "1");

        root.Elements(S + "rowBreaks").Remove();
        root.Elements(S + "colBreaks").Remove();

        var pageSetup = root.Element(S + "pageSetup");
        if (pageSetup is null)
        {
            pageSetup = new XElement(S + "pageSetup");
            var previous = root.Element(S + "pageMargins") ?? root.Element(S + "printOptions");
            if (previous is not null)
            {
                previous.AddAfterSelf(pageSetup);
            }
            else
            {
                var next = root.Elements().FirstOrDefault(e => PageSetupFollowers.Contains(e.Name.LocalName));
                if (next is not null)
                {
                    next.AddBeforeSelf(pageSetup);
                }
                else
                {
                    root.Add(pageSetup);
                }
            }
        }

        pageSetup.Attribute("scale")?.Remove();
        pageSetup.SetAttributeValue("fitToWidth", "1");
        pageSetup.SetAttributeValue("fitToHeight", "1");
    }

    private static readonly HashSet<string> PageSetupFollowers =
    [
        "headerFooter", "customProperties", "cellWatches", "ignoredErrors", "smartTags", "drawing",
        "legacyDrawing", "legacyDrawingHF", "drawingHF", "picture", "oleObjects", "controls", "webPublishItems",
        "tableParts", "extLst",
    ];

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void Save(OpenXmlPart part, XDocument xml)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        xml.Save(stream, SaveOptions.DisableFormatting);
    }
}
