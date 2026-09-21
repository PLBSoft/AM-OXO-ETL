using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class HeaderFieldRuleTests
{
    private const string Range = "M2:O2";

    [Fact]
    public void Constructor_WithNameAndCellOnly_UsesFalseAndNullDefaults()
    {
        var rule = new HeaderFieldRule("nomMAD", Range);

        rule.Name.Should().Be("nomMAD");
        rule.CellRange.Should().Be(Range);
        rule.StripReperePrefix.Should().BeFalse();
        rule.DateFormat.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithStripReperePrefixAndDateFormat_AssignsBothProperties()
    {
        var rule = new HeaderFieldRule("dateRev", Range, stripReperePrefix: true, dateFormat: "dd/MM/yyyy");

        rule.StripReperePrefix.Should().BeTrue();
        rule.DateFormat.Should().Be("dd/MM/yyyy");
    }

    [Fact]
    public void Equality_WithSameValues_IsStructural()
    {
        var a = new HeaderFieldRule("nomMAD", Range, stripReperePrefix: true);
        var b = new HeaderFieldRule("nomMAD", Range, stripReperePrefix: true);

        a.Should().Be(b);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new HeaderFieldRule(invalidName!, Range);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderFieldRule_EmptyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankDateFormat_ThrowsDomainValidationException(string blankDateFormat)
    {
        var act = () => new HeaderFieldRule("dateRev", Range, dateFormat: blankDateFormat);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("dateFormat")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderFieldRule_BlankDateFormat);
    }

    // Lot 084 (G13): the range validation of the former DirectCell.
    [Theory]
    [InlineData("M2")]
    [InlineData("M2:O2")]
    [InlineData("K6:T6")]
    public void Constructor_WithValidRange_KeepsIt(string range) =>
        new HeaderFieldRule("nomMAD", range).CellRange.Should().Be(range);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("M2:")]
    [InlineData("M2-O2")]
    public void Constructor_WithInvalidRange_ThrowsDomainValidationException(string? invalidRange)
    {
        var act = () => new HeaderFieldRule("nomMAD", invalidRange!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("cellRange")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderFieldRule_InvalidCellRange);
    }
}
