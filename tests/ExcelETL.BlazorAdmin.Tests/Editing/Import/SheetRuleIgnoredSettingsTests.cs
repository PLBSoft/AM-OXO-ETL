using ExcelETL.BlazorAdmin.Editing.Import;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing.Import;

// What the import editor warns about: the settings of a sheet rule that this sheet's extraction never uses
// (ImportSheetUsage, the same table as the Details page).
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
        result.ConditionalPointRulesIgnored.Should().BeFalse();
        result.FieldPresencePointRulesIgnored.Should().BeFalse();
        result.HeaderRulesIgnored.Should().BeFalse();
        result.AnyConfiguredSettingIgnored.Should().BeTrue();
    }

    // Lot 084.6: the five element sheets read conditional rules and headers; no sheet reads the
    // "filled cell" rules any more (removed in 84.8).
    [Theory]
    [InlineData("PLATINES", false, true, false)]
    [InlineData("ORIFICES CAPACITES", false, true, false)]
    [InlineData("ISOLEMENT", false, true, false)]
    [InlineData("PROCEDURE", false, true, false)]
    [InlineData("AUTRES JOINTS TOUCHES", false, true, false)]
    [InlineData("DIVERS", false, true, false)]
    public void Sections_AreReportedAsIgnored_ForTheSheetsThatDontReadThem(
        string sheetName, bool conditionalIgnored, bool fieldPresenceIgnored, bool headerIgnored)
    {
        var result = SheetRuleIgnoredSettings.For(Rule(sheetName));

        result.SheetNotProcessed.Should().BeFalse();
        result.ConditionalPointRulesIgnored.Should().Be(conditionalIgnored);
        result.FieldPresencePointRulesIgnored.Should().Be(fieldPresenceIgnored);
        result.HeaderRulesIgnored.Should().Be(headerIgnored);
    }

    // The client's case (ticket J2M76): conditional rules on PLATINES are read since lot 084.6.
    [Fact]
    public void ConditionalRulesOnPlatines_AreNoLongerAnIgnoredSetting()
    {
        var rule = Rule("PLATINES");
        rule.PointRules.Add(new ConditionalPointRuleDraft { ColonneName = "DEB MAD", SourceFieldName = "HasDebMad", ComparisonValue = "DEBUT MAD" });

        SheetRuleIgnoredSettings.For(rule).AnyConfiguredSettingIgnored.Should().BeFalse();
    }

    [Fact]
    public void EmptyIgnoredSections_AreNotAConfiguredIgnoredSetting()
    {
        SheetRuleIgnoredSettings.For(Rule("PLATINES")).AnyConfiguredSettingIgnored.Should().BeFalse();
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

    // Lot 084 (G5): zéro énergie is an ordinary point rule; the dedicated value is read by no sheet.
    [Theory]
    [InlineData("ISOLEMENT", true)]
    [InlineData("PLATINES", true)]
    [InlineData("PROCEDURE", true)]
    public void ZeroEnergieValue_IsIgnoredOnEverySheet_OnlyWhenFilledIn(string sheetName, bool expectedIgnored)
    {
        SheetRuleIgnoredSettings.For(Rule(sheetName)).ZeroEnergieExpectedValueIgnored.Should().BeFalse();

        var rule = Rule(sheetName);
        rule.ZeroEnergieExpectedValue = "ZERO ENERGIE";

        SheetRuleIgnoredSettings.For(rule).ZeroEnergieExpectedValueIgnored.Should().Be(expectedIgnored);
    }

    // Lot 084.6: every element sheet reads a colour; only PROCEDURE doesn't.
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

    // Lot 084.6 (G10): the dedicated couleur cell is read by no sheet any more (removed in 84.8).
    [Theory]
    [InlineData("PLATINES")]
    [InlineData("ORIFICES CAPACITES")]
    [InlineData("PROCEDURE")]
    public void DedicatedCouleurCell_IsIgnoredOnEverySheet_OnlyWhenFilledIn(string sheetName)
    {
        SheetRuleIgnoredSettings.For(Rule(sheetName)).CouleurEtiquetteCellIgnored.Should().BeFalse();

        var rule = Rule(sheetName);
        rule.CouleurEtiquetteCellRange = "H18:N18";

        var result = SheetRuleIgnoredSettings.For(rule);
        result.CouleurEtiquetteCellIgnored.Should().BeTrue();
        result.AnyConfiguredSettingIgnored.Should().BeTrue();
    }
}
