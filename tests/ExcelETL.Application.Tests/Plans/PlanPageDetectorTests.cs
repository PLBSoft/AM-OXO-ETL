using ExcelETL.Application.Plans;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.3): pages rebuilt from the repeated
// cartouche logo, empty pages dropped, width covering every drawing. No file involved.
public class PlanPageDetectorTests
{
    private const string Logo = "logo";

    private static PlanSheetAnchor LogoAt(int row) => new(PlanAnchorKind.Picture, row, row + 2, 3, Logo);

    private static PlanSheetAnchor PhotoAt(int row, int toColumn = 10, string key = "photo") =>
        new(PlanAnchorKind.Picture, row, row + 20, toColumn, key);

    private static PlanSheetAnchor ShapeAt(int row, int toColumn = 8) => new(PlanAnchorKind.Shape, row, row + 1, toColumn);

    private static PlanSheetLayout Layout(int lastUsedRow, params PlanSheetAnchor[] anchors) =>
        new(anchors, HeaderLastColumn: 12, LastUsedRow: lastUsedRow, LastUsedColumn: 12);

    [Fact]
    public void RepeatedLogo_SplitsTheSheetIntoPages_EachEndingTheRowBeforeTheNextLogo()
    {
        var result = PlanPageDetector.Detect(Layout(165,
            LogoAt(2), PhotoAt(10), LogoAt(57), ShapeAt(70), LogoAt(113), PhotoAt(120)));

        result.Pages.Should().Equal(new PlanPage(2, 56), new PlanPage(57, 112), new PlanPage(113, 165));
    }

    [Fact]
    public void LastPage_EndsAtTheLowestDrawingWhenItGoesBelowTheUsedRange()
    {
        var result = PlanPageDetector.Detect(Layout(60, LogoAt(2), PhotoAt(50)));

        result.Pages.Should().Equal(new PlanPage(2, 70));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EmptyPage_IsDropped_WhereverItIs(int emptyPageIndex)
    {
        var logoRows = new[] { 2, 57, 113 };
        var anchors = new List<PlanSheetAnchor>();
        for (var i = 0; i < logoRows.Length; i++)
        {
            anchors.Add(LogoAt(logoRows[i]));
            if (i != emptyPageIndex)
            {
                anchors.Add(ShapeAt(logoRows[i] + 10));
            }
        }

        var result = PlanPageDetector.Detect(Layout(165, [.. anchors]));

        result.Pages.Should().HaveCount(2);
        result.Pages.Select(p => p.FirstRow).Should().NotContain(logoRows[emptyPageIndex]);
    }

    [Fact]
    public void EveryPageEmpty_GivesNoPage()
    {
        var result = PlanPageDetector.Detect(Layout(165, LogoAt(2), LogoAt(57), LogoAt(113)));

        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public void NoLogo_WithADrawing_IsOnePageCoveringTheWholeSheet()
    {
        var result = PlanPageDetector.Detect(Layout(40, PhotoAt(5)));

        result.Pages.Should().Equal(new PlanPage(1, 40));
    }

    [Fact]
    public void NoLogo_NoDrawing_GivesNoPage()
    {
        var result = PlanPageDetector.Detect(Layout(40));

        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public void Width_IsExtendedToTheRightMostDrawing()
    {
        var result = PlanPageDetector.Detect(Layout(60, LogoAt(2), ShapeAt(10, toColumn: 17)));

        result.LastColumn.Should().Be(17);
    }

    [Fact]
    public void Width_IsAtLeastTheCartouche()
    {
        var result = PlanPageDetector.Detect(Layout(60, LogoAt(2), ShapeAt(10, toColumn: 5)));

        result.LastColumn.Should().Be(12);
    }

    [Fact]
    public void Width_FallsBackToTheUsedRange_WithoutCartoucheOrDrawing()
    {
        var result = PlanPageDetector.Detect(new PlanSheetLayout([], 0, 30, 9));

        result.LastColumn.Should().Be(9);
    }

    // A second picture in the cartouche (e.g. a customer logo next to ours) repeats on every page: it is
    // part of the cartouche, it neither starts a page nor makes a page non-empty.
    [Fact]
    public void OtherPictureInTheCartouche_DoesNotStartAPage_NorCountAsContent()
    {
        PlanSheetAnchor SecondLogoAt(int row) => new(PlanAnchorKind.Picture, row + 1, row + 2, 11, "client-logo");

        var result = PlanPageDetector.Detect(Layout(165,
            LogoAt(2), SecondLogoAt(2), ShapeAt(10),
            LogoAt(57), SecondLogoAt(57),
            LogoAt(113), SecondLogoAt(113), PhotoAt(120)));

        result.Pages.Should().Equal(new PlanPage(2, 56), new PlanPage(113, 165));
    }

    [Fact]
    public void PictureOfADifferentImage_IsContent_EvenNextToTheLogo()
    {
        var result = PlanPageDetector.Detect(Layout(110, LogoAt(2), LogoAt(57), PhotoAt(57)));

        result.Pages.Should().Equal(new PlanPage(57, 110));
    }

    [Fact]
    public void DrawingAboveTheFirstLogo_BelongsToTheFirstPage()
    {
        var result = PlanPageDetector.Detect(Layout(110, ShapeAt(1), LogoAt(3), LogoAt(57)));

        result.Pages.Should().Equal(new PlanPage(1, 56));
    }

    [Fact]
    public void SameRowLogoTwice_StartsOnlyOnePage()
    {
        var result = PlanPageDetector.Detect(Layout(60, LogoAt(2), LogoAt(2), ShapeAt(10)));

        result.Pages.Should().Equal(new PlanPage(2, 60));
    }
}
