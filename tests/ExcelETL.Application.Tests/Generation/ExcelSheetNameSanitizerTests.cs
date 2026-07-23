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
}
