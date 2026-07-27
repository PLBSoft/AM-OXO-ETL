using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class HeaderFieldRuleTests
{
    private static DirectCell Cell() => new("PROCEDURE", "M2:O2");

    [Fact]
    public void Constructor_WithNameAndCellOnly_UsesFalseAndNullDefaults()
    {
        var rule = new HeaderFieldRule("nomMAD", Cell());

        rule.Name.Should().Be("nomMAD");
        rule.Cell.Should().Be(Cell());
        rule.StripReperePrefix.Should().BeFalse();
        rule.DateFormat.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithStripReperePrefixAndDateFormat_AssignsBothProperties()
    {
        var rule = new HeaderFieldRule("dateRev", Cell(), stripReperePrefix: true, dateFormat: "dd/MM/yyyy");

        rule.StripReperePrefix.Should().BeTrue();
        rule.DateFormat.Should().Be("dd/MM/yyyy");
    }

    [Fact]
    public void Equality_WithSameValues_IsStructural()
    {
        var a = new HeaderFieldRule("nomMAD", Cell(), stripReperePrefix: true);
        var b = new HeaderFieldRule("nomMAD", Cell(), stripReperePrefix: true);

        a.Should().Be(b);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new HeaderFieldRule(invalidName!, Cell());

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderFieldRule_EmptyName);
    }

    [Fact]
    public void Constructor_WithNullCell_ThrowsArgumentNullException()
    {
        var act = () => new HeaderFieldRule("nomMAD", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankDateFormat_ThrowsDomainValidationException(string blankDateFormat)
    {
        var act = () => new HeaderFieldRule("dateRev", Cell(), dateFormat: blankDateFormat);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("dateFormat")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderFieldRule_BlankDateFormat);
    }
}
