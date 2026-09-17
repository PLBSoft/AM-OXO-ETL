using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

// Lot 080.1 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md): the sheet names
// ClosedXML refuses, now owned by the domain. ExcelSheetNameWriterAgreementTests (Infrastructure.Tests)
// checks these rules against the real writer.
public class ExcelSheetNameTests
{
    [Fact]
    public void MaxLength_Is31() => ExcelSheetName.MaxLength.Should().Be(31);

    [Fact]
    public void IsTooLong_With31Characters_ReturnsFalse() =>
        ExcelSheetName.IsTooLong(new string('A', 31)).Should().BeFalse();

    [Fact]
    public void IsTooLong_With32Characters_ReturnsTrue() =>
        ExcelSheetName.IsTooLong(new string('A', 32)).Should().BeTrue();

    [Theory]
    [InlineData('\\')]
    [InlineData('/')]
    [InlineData('?')]
    [InlineData('*')]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData(':')]
    public void ForbiddenCharactersIn_WithEachForbiddenCharacter_ReturnsIt(char forbidden)
    {
        var name = $"A{forbidden}B";

        ExcelSheetName.ForbiddenCharactersIn(name).Should().Equal(forbidden);
        ExcelSheetName.IsValid(name).Should().BeFalse();
    }

    [Fact]
    public void ForbiddenCharactersIn_ReturnsDistinctCharactersInOrderOfFirstAppearance() =>
        ExcelSheetName.ForbiddenCharactersIn("a:b/c:d/e?").Should().Equal(':', '/', '?');

    [Fact]
    public void ForbiddenCharactersIn_WithNoForbiddenCharacter_ReturnsEmpty() =>
        ExcelSheetName.ForbiddenCharactersIn("Parents - Tâches (1)").Should().BeEmpty();

    [Theory]
    [InlineData("'Parents", true)]
    [InlineData("Parents'", true)]
    [InlineData("Parent's", false)]
    [InlineData("Parents", false)]
    public void HasApostropheAtEdge_OnlyAtTheStartOrTheEnd(string name, bool expected) =>
        ExcelSheetName.HasApostropheAtEdge(name).Should().Be(expected);

    [Theory]
    [InlineData("Parents")]
    [InlineData(" Parents ")]
    [InlineData("Parent's")]
    public void IsValid_WithAnAcceptedName_ReturnsTrue(string name) =>
        ExcelSheetName.IsValid(name).Should().BeTrue();

    [Theory]
    [InlineData("'Parents")]
    [InlineData("Parents'")]
    public void IsValid_WithApostropheAtEdge_ReturnsFalse(string name) =>
        ExcelSheetName.IsValid(name).Should().BeFalse();

    [Fact]
    public void IsValid_With32Characters_ReturnsFalse() =>
        ExcelSheetName.IsValid(new string('A', 32)).Should().BeFalse();

    [Fact]
    public void NameComparer_IgnoresCase() =>
        ExcelSheetName.NameComparer.Equals("Parents", "PARENTS").Should().BeTrue();

    [Fact]
    public void NameComparer_DoesNotIgnoreSurroundingSpaces() =>
        ExcelSheetName.NameComparer.Equals("Parents", " Parents ").Should().BeFalse();
}
