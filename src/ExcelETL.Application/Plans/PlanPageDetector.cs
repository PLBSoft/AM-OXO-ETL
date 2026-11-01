namespace ExcelETL.Application.Plans;

// Lot 091 (091.3, decisions D4/D9): the plan sheets have no page break or print area; the client fakes
// pages by repeating a header block (cartouche) whose logo is the same picture every time. A page starts
// at each copy of that logo -- the top-left picture anchored in rows 1-4 -- and runs to the row before the
// next one. Pictures anchored in rows 1-4 belong to the cartouche: never content. A page is kept only when
// another picture or a shape is anchored in it. Without any logo, the whole sheet is one page if it holds
// a drawing. The printed width covers the cartouche and every drawing (annotations overflow to the right).
public static class PlanPageDetector
{
    public const int HeaderRowCount = 4;

    public static PlanSheetPrintLayout Detect(PlanSheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var headerPictures = layout.Anchors
            .Where(a => a.Kind == PlanAnchorKind.Picture && a.FromRow <= HeaderRowCount && a.ImageKey is not null)
            .ToList();
        var cartoucheImageKeys = headerPictures.Select(a => a.ImageKey!).ToHashSet(StringComparer.Ordinal);
        var logoKey = headerPictures
            .OrderBy(a => a.FromRow)
            .ThenBy(a => a.ToColumn)
            .Select(a => a.ImageKey)
            .FirstOrDefault();

        var contentRows = layout.Anchors
            .Where(a => a.Kind == PlanAnchorKind.Shape || a.ImageKey is null || !cartoucheImageKeys.Contains(a.ImageKey))
            .Select(a => a.FromRow)
            .ToList();

        var lastRow = Math.Max(layout.LastUsedRow, layout.Anchors.Select(a => a.ToRow).DefaultIfEmpty(0).Max());
        var lastColumn = new[] { layout.HeaderLastColumn, layout.Anchors.Select(a => a.ToColumn).DefaultIfEmpty(0).Max() }.Max();
        if (lastColumn == 0)
        {
            lastColumn = Math.Max(layout.LastUsedColumn, 1);
        }

        var starts = layout.Anchors
            .Where(a => a.Kind == PlanAnchorKind.Picture && logoKey is not null && a.ImageKey == logoKey)
            .Select(a => a.FromRow)
            .Distinct()
            .Order()
            .ToList();

        var pages = new List<PlanPage>();
        if (starts.Count == 0)
        {
            if (contentRows.Count > 0)
            {
                pages.Add(new PlanPage(1, Math.Max(lastRow, 1)));
            }

            return new PlanSheetPrintLayout(pages, lastColumn);
        }

        for (var i = 0; i < starts.Count; i++)
        {
            // Drawings above the first logo belong to the first page.
            var firstRow = i == 0 ? Math.Min(starts[0], contentRows.DefaultIfEmpty(starts[0]).Min()) : starts[i];
            var pageLastRow = i + 1 < starts.Count ? starts[i + 1] - 1 : Math.Max(lastRow, starts[i]);
            var page = new PlanPage(firstRow, pageLastRow);
            if (contentRows.Any(row => row >= page.FirstRow && row <= page.LastRow))
            {
                pages.Add(page);
            }
        }

        return new PlanSheetPrintLayout(pages, lastColumn);
    }
}
