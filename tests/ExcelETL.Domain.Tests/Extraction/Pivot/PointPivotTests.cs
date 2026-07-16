using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class PointPivotTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesPointPivot()
    {
        var point = new PointPivot("PROLOCK VANNES", "C7401-ISO1");

        point.ColonneNom.Should().Be("PROLOCK VANNES");
        point.ParentRepere.Should().Be("C7401-ISO1");
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new PointPivot("PROLOCK VANNES", "C7401-ISO1");
        var second = new PointPivot("PROLOCK VANNES", "C7401-ISO1");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidColonneNom_ThrowsDomainValidationException(string? invalidColonneNom)
    {
        var act = () => new PointPivot(invalidColonneNom!, "C7401-ISO1");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("colonneNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.PointPivot_EmptyColonneNom);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidParentRepere_ThrowsDomainValidationException(string? invalidParentRepere)
    {
        var act = () => new PointPivot("PROLOCK VANNES", invalidParentRepere!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("parentRepere")
            .Which.ErrorCode.Should().Be(DomainErrorCode.PointPivot_EmptyParentRepere);
    }
}
