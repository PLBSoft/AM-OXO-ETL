using ExcelETL.BlazorAdmin.Formatting;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.2 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExcelColumnLettersTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    [InlineData(16384, "XFD")]
    public void FromNumber_ReturnsTheExcelColumnLetters(int number, string expected) =>
        ExcelColumnLetters.FromNumber(number).Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromNumber_WithZeroOrNegative_Throws(int number) =>
        FluentActions.Invoking(() => ExcelColumnLetters.FromNumber(number)).Should().Throw<ArgumentOutOfRangeException>();
}
