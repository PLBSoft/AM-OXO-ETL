using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

// Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md).
public class TacheMultipleTypeLabelTests
{
    [Fact]
    public void Constructor_WithValidCodeAndLabel_CreatesRealInstance()
    {
        var label = new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD");

        label.Code.Should().Be("TM_PROC_MAD");
        label.Label.Should().Be("Procédure MAD");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidCode_ThrowsDomainValidationException(string? invalidCode)
    {
        var act = () => new TacheMultipleTypeLabel(invalidCode!, "Procédure MAD");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("code")
            .Which.ErrorCode.Should().Be(DomainErrorCode.TacheMultipleTypeLabel_EmptyCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidLabel_ThrowsDomainValidationException(string? invalidLabel)
    {
        var act = () => new TacheMultipleTypeLabel("TM_PROC_MAD", invalidLabel!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("label")
            .Which.ErrorCode.Should().Be(DomainErrorCode.TacheMultipleTypeLabel_EmptyLabel);
    }

    [Fact]
    public void Constructor_WithCodeExceedingMaxLength_ThrowsDomainValidationException()
    {
        var tooLong = new string('A', ImportProfile.MaxListItemNameLength + 1);

        var act = () => new TacheMultipleTypeLabel(tooLong, "Procédure MAD");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("code")
            .Which.ErrorCode.Should().Be(DomainErrorCode.TacheMultipleTypeLabel_CodeTooLong);
    }

    [Fact]
    public void Constructor_WithLabelExceedingMaxLength_ThrowsDomainValidationException()
    {
        var tooLong = new string('A', ImportProfile.MaxListItemNameLength + 1);

        var act = () => new TacheMultipleTypeLabel("TM_PROC_MAD", tooLong);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("label")
            .Which.ErrorCode.Should().Be(DomainErrorCode.TacheMultipleTypeLabel_LabelTooLong);
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD");
        var second = new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
