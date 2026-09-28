using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class ExtractionErrorTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesExtractionError()
    {
        var error = new ExtractionError(
            "ISOLEMENT", "C7401-ISO3", ExtractionErrorCode.RequiredFieldMissing, "Cellule H18 introuvable ou vide.");

        error.Sheet.Should().Be("ISOLEMENT");
        error.BlockIdentifier.Should().Be("C7401-ISO3");
        error.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        error.Message.Should().Be("Cellule H18 introuvable ou vide.");
        error.ExtractedValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExtractedValue_ExposesItAsAStructuredField()
    {
        var error = new ExtractionError(
            "ISOLEMENT", "C7401-V1", ExtractionErrorCode.NoConditionalPointCreated, "Message", extractedValue: "PROLOCK");

        error.ExtractedValue.Should().Be("PROLOCK");
    }

    // Lot 085.1: blocking code for two elements of one file sharing the same generated repère.
    [Fact]
    public void Constructor_WithDuplicateRepereCode_KeepsTheDuplicatedRepereAsExtractedValue()
    {
        var error = new ExtractionError(
            "DIVERS", "18", ExtractionErrorCode.DuplicateRepere, "Message", extractedValue: "LRS4504-LRS4504");

        error.Code.Should().Be(ExtractionErrorCode.DuplicateRepere);
        error.ExtractedValue.Should().Be("LRS4504-LRS4504");
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new ExtractionError("ISOLEMENT", "C7401-ISO3", ExtractionErrorCode.RequiredFieldMissing, "Message");
        var second = new ExtractionError("ISOLEMENT", "C7401-ISO3", ExtractionErrorCode.RequiredFieldMissing, "Message");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheet_ThrowsDomainValidationException(string? invalidSheet)
    {
        var act = () => new ExtractionError(invalidSheet!, "C7401-ISO3", ExtractionErrorCode.RequiredFieldMissing, "Message");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheet")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionError_EmptySheet);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidBlockIdentifier_ThrowsDomainValidationException(string? invalidBlockIdentifier)
    {
        var act = () => new ExtractionError("ISOLEMENT", invalidBlockIdentifier!, ExtractionErrorCode.RequiredFieldMissing, "Message");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("blockIdentifier")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionError_EmptyBlockIdentifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidMessage_ThrowsDomainValidationException(string? invalidMessage)
    {
        var act = () => new ExtractionError("ISOLEMENT", "C7401-ISO3", ExtractionErrorCode.RequiredFieldMissing, invalidMessage!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("message")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionError_EmptyMessage);
    }
}
