using ExcelETL.BlazorAdmin.Editing.Import;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing.Import;

// What the import editor warns about (ImportSheetUsage, the same table as the Details page). Since lot
// 084.7: an unprocessed sheet name, what PROCEDURE ignores, and colour combinations with no effect.
public class SheetRuleIgnoredSettingsTests
{
    private static SheetExtractionRuleDraft Rule(string sheetName) => new() { SheetName = sheetName };

    [Fact]
    public void BlankSheetName_ReportsNothing()
    {
        var result = SheetRuleIgnoredSettings.For(Rule("  "));

        result.Should().BeEquivalentTo(SheetRuleIgnoredSettings.None);
    }

    [Fact]
    public void UnknownSheetName_ReportsOnlyThatTheSheetIsNotProcessed()
    {
        var result = SheetRuleIgnoredSettings.For(Rule("platines"));

        result.SheetNotProcessed.Should().BeTrue();
        result.IgnoredStopFieldFixedName.Should().BeNull();
        result.CouleurEtiquetteIgnored.Should().BeFalse();
        result.AnyConfiguredSettingIgnored.Should().BeTrue();
    }

    // Client ticket J2M76: the client's conditional rules on PLATINES are read since lot 084.6.
    [Theory]
    [InlineData("PROCEDURE")]
    [InlineData("ISOLEMENT")]
    [InlineData("PLATINES")]
    [InlineData("ORIFICES CAPACITES")]
    [InlineData("AUTRES JOINTS TOUCHES")]
    [InlineData("DIVERS")]
    public void PointRulesAndHeaders_AreNeverAnIgnoredSetting(string sheetName)
    {
        var rule = Rule(sheetName);
        rule.PointRules.Add(new ConditionalPointRuleDraft { ColonneName = "DEB MAD", SourceFieldName = "HasDebMad", ComparisonValue = "DEBUT MAD" });
        rule.HeaderFields.Add(new HeaderFieldRuleDraft { Name = "repereEcho", Range = "N6" });

        SheetRuleIgnoredSettings.For(rule).AnyConfiguredSettingIgnored.Should().BeFalse();
    }

    [Theory]
    [InlineData("PROCEDURE", "Action", null)]
    [InlineData("PROCEDURE", "Ordre", "Action")]
    [InlineData("ISOLEMENT", "TypeElement", null)]
    [InlineData("PLATINES", "Anything", null)]
    [InlineData("PROCEDURE", "", null)]
    public void StopField_IsIgnored_WhenTheSheetAlwaysStopsOnAnotherField(string sheetName, string stopField, string? expectedFixedName)
    {
        var rule = Rule(sheetName);
        rule.StopFieldName = stopField;

        var result = SheetRuleIgnoredSettings.For(rule);

        result.IgnoredStopFieldFixedName.Should().Be(expectedFixedName);
        result.AnyConfiguredSettingIgnored.Should().Be(expectedFixedName is not null);
    }

    // Every element sheet reads a colour; only PROCEDURE doesn't.
    [Theory]
    [InlineData("PROCEDURE", true)]
    [InlineData("ISOLEMENT", false)]
    [InlineData("DIVERS", false)]
    [InlineData("PLATINES", false)]
    [InlineData("AUTRES JOINTS TOUCHES", false)]
    public void CouleurSettings_AreIgnoredOnSheetsThatReadNoColour_OnlyWhenFilledIn(string sheetName, bool expectedIgnored)
    {
        SheetRuleIgnoredSettings.For(Rule(sheetName)).CouleurEtiquetteIgnored.Should().BeFalse();

        var rule = Rule(sheetName);
        rule.DefaultCouleurEtiquette = "BLEUE";

        SheetRuleIgnoredSettings.For(rule).CouleurEtiquetteIgnored.Should().Be(expectedIgnored);
    }

    // Same rules as ElementSheetExtractionService (lot 084, G10): a "CouleurEtiquette" block field wins over
    // the default; without one the allowed list has nothing to filter.
    [Fact]
    public void OnASheetReadingColour_DefaultIsIgnoredWithACell_AndAllowedListIsIgnoredWithoutOne()
    {
        var withCell = Rule("PLATINES");
        withCell.Fields.Add(new BlockFieldDefinitionDraft { Name = "CouleurEtiquette", AbsoluteRange = "H18:N18" });
        withCell.DefaultCouleurEtiquette = "ROUGE";
        withCell.AllowedCouleursEtiquette = "ROUGE, BLANC";

        var withCellResult = SheetRuleIgnoredSettings.For(withCell);
        withCellResult.DefaultCouleurIgnoredBecauseCell.Should().BeTrue();
        withCellResult.AllowedCouleursIgnoredWithoutCell.Should().BeFalse();
        withCellResult.CouleurEtiquetteIgnored.Should().BeFalse();

        var withoutCell = Rule("PLATINES");
        withoutCell.DefaultCouleurEtiquette = "ROUGE";
        withoutCell.AllowedCouleursEtiquette = "ROUGE, BLANC";

        var withoutCellResult = SheetRuleIgnoredSettings.For(withoutCell);
        withoutCellResult.DefaultCouleurIgnoredBecauseCell.Should().BeFalse();
        withoutCellResult.AllowedCouleursIgnoredWithoutCell.Should().BeTrue();
    }

    [Fact]
    public void CouleurField_StillBeingTyped_CountsAsTheCell()
    {
        var rule = Rule("PLATINES");
        rule.PendingField.Name = "CouleurEtiquette";
        rule.DefaultCouleurEtiquette = "ROUGE";

        SheetRuleIgnoredSettings.For(rule).DefaultCouleurIgnoredBecauseCell.Should().BeTrue();
    }
}
