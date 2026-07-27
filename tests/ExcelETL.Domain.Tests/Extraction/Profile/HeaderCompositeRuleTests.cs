using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class HeaderCompositeRuleTests
{
    [Fact]
    public void Constructor_WithValidArguments_AssignsProperties()
    {
        var rule = new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}");

        rule.Name.Should().Be("Designation");
        rule.Template.Should().Be("Rév {revision} du {dateRev}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new HeaderCompositeRule(invalidName!, "Rév {revision}");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderCompositeRule_EmptyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTemplate_ThrowsDomainValidationException(string? invalidTemplate)
    {
        var act = () => new HeaderCompositeRule("Designation", invalidTemplate!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("template")
            .Which.ErrorCode.Should().Be(DomainErrorCode.HeaderCompositeRule_EmptyTemplate);
    }

    [Fact]
    public void PlaceholderNames_WithMultiplePlaceholders_ReturnsDistinctNamesInOrder()
    {
        var rule = new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev} (rev {revision})");

        rule.PlaceholderNames().Should().Equal("revision", "dateRev");
    }

    [Fact]
    public void PlaceholderNames_WithNoPlaceholders_ReturnsEmpty()
    {
        var rule = new HeaderCompositeRule("Fixed", "Literal text");

        rule.PlaceholderNames().Should().BeEmpty();
    }
}
