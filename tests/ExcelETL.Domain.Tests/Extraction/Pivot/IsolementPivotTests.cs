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
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "FERMÉE", "Zone A");

        isolement.Repere.Should().Be("C7401-ISO1");
        isolement.Designation.Should().Be("Vanne principale");
        isolement.TypeElementNom.Should().Be("ZERO ENERGIE");
        isolement.PositionALaPose.Should().Be("FERMÉE");
        isolement.Localisation.Should().Be("Zone A");
    }

    [Fact]
    public void Constructor_WithBlankDesignation_CreatesIsolementPivot()
    {
        // Real D8570 fixture, ISOLEMENT sheet, Identification "V4"/TypeElement "VANNE": Designation
        // is blank -- must still be extracted normally (unrecognized TypeElement is a non-blocking
        // warning per spec, not a rejection), so Designation is deliberately unvalidated here.
        var isolement = new IsolementPivot("D8570-V4", "", "VANNE", "FERMÉE", "");

        isolement.Designation.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyLocalisation_CreatesIsolementPivot()
    {
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "FERMÉE", string.Empty);

        isolement.Localisation.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "FERMÉE", "Zone A");
        var second = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "FERMÉE", "Zone A");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void WithExpression_CanBroadcastLocalisation()
    {
        var isolement = new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", "FERMÉE", string.Empty);

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
        var act = () => new IsolementPivot(invalidRepere!, "Vanne principale", "ZERO ENERGIE", "FERMÉE", "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("repere")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyRepere);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTypeElementNom_ThrowsDomainValidationException(string? invalidTypeElementNom)
    {
        var act = () => new IsolementPivot("C7401-ISO1", "Vanne principale", invalidTypeElementNom!, "FERMÉE", "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("typeElementNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyTypeElementNom);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidPositionALaPose_ThrowsDomainValidationException(string? invalidPositionALaPose)
    {
        var act = () => new IsolementPivot("C7401-ISO1", "Vanne principale", "ZERO ENERGIE", invalidPositionALaPose!, "Zone A");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("positionALaPose")
            .Which.ErrorCode.Should().Be(DomainErrorCode.IsolementPivot_EmptyPositionALaPose);
    }
}
