using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class IsolementPivotTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesIsolementPivot()
    {
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "Zone A");

        isolement.Repere.Should().Be("C7401-ISO1");
        isolement.Designation.Should().Be("Vanne principale");
        isolement.TypeElementNom.Should().Be("ZERO ENERGIE");
        isolement.Localisation.Should().Be("Zone A");
    }

    [Fact]
    public void Constructor_WithEmptyLocalisation_CreatesIsolementPivot()
    {
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", string.Empty);

        isolement.Localisation.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "Zone A");
        var second = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "Zone A");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void WithExpression_CanBroadcastLocalisation()
    {
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", string.Empty);

        var broadcast = isolement with { Localisation = "Zone A" };

        broadcast.Localisation.Should().Be("Zone A");
        broadcast.Repere.Should().Be(isolement.Repere);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidRepere_ThrowsDomainValidationException(string? invalidRepere)
    {
        var act = () => new IsolementPivot(invalidRepere!, "Vanne principale", "ZERO ENERGIE", "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("repere")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyRepere);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidDesignation_ThrowsDomainValidationException(string? invalidDesignation)
    {
        var act = () => new IsolementPivot("C7401-ISO1", invalidDesignation!, "ZERO ENERGIE", "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("designation")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyDesignation);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTypeElementNom_ThrowsDomainValidationException(string? invalidTypeElementNom)
    {
        var act = () => new IsolementPivot("C7401-ISO1", "Vanne principale", invalidTypeElementNom!, "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("typeElementNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyTypeElementNom);
    }
}
