using ExcelETL.Application.Generation;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Generation;

public class ExcelSheetNameSanitizerTests
{
    [Theory]
    [InlineData("TM/PROC:MAD", "TM_PROC_MAD")]
    [InlineData("TM\\PROC?MAD", "TM_PROC_MAD")]
    [InlineData("TM*PROC[MAD]", "TM_PROC_MAD_")]
    public void Sanitize_WithForbiddenCharacters_ReplacesThemWithUnderscore(string rawName, string expected)
    {
        ExcelSheetNameSanitizer.Sanitize(rawName).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_WithNameLongerThan31Characters_TruncatesTo31Characters()
    {
        var longName = new string('A', 40);

        var result = ExcelSheetNameSanitizer.Sanitize(longName);

        result.Should().HaveLength(31);
        result.Should().Be(longName[..31]);
    }

    [Theory]
    [InlineData("TM_PROC_MAD")]
    [InlineData("TM_PROC_REL")]
    public void Sanitize_WithKnownRealCodes_LeavesThemUnmodified(string realCode)
    {
        ExcelSheetNameSanitizer.Sanitize(realCode).Should().Be(realCode);
    }

    // Lot 080.5 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md, D6): the remaining
    // names Excel refuses.
    [Theory]
    [InlineData("'TM'", "TM")]
    [InlineData("'TM_PROC_MAD", "TM_PROC_MAD")]
    [InlineData("TM's", "TM's")]
    public void Sanitize_WithApostropheAtEdge_RemovesIt(string rawName, string expected)
    {
        ExcelSheetNameSanitizer.Sanitize(rawName).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("''")]
    public void Sanitize_WhenNothingIsLeft_ReturnsUnderscore(string rawName)
    {
        ExcelSheetNameSanitizer.Sanitize(rawName).Should().Be("_");
    }

    [Fact]
    public void Sanitize_WhenTruncationLeavesATrailingApostrophe_RemovesIt()
    {
        var rawName = new string('A', 30) + "'B";

        ExcelSheetNameSanitizer.Sanitize(rawName).Should().Be(new string('A', 30));
    }
}
