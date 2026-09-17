using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Generation;

// Lot 080.5 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md, D4).
public class GeneratedSheetNameValidatorTests
{
    [Fact]
    public void Validate_WithValidDistinctNames_DoesNotThrow()
    {
        var act = () => GeneratedSheetNameValidator.Validate(["Parents", "Enfants", "TM_PROC_MAD", " Parents "]);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("A/B")]
    [InlineData("'Parents")]
    public void Validate_WithANameExcelRefuses_ThrowsGeneratedSheetNameInvalid(string invalidName)
    {
        var act = () => GeneratedSheetNameValidator.Validate(["Parents", invalidName]);

        var exception = act.Should().Throw<GeneratedSheetNameException>().Which;
        exception.ErrorCode.Should().Be(ApplicationErrorCode.GeneratedSheetNameInvalid);
        exception.SheetName.Should().Be(invalidName);
        exception.Args.Should().Equal(invalidName);
        exception.ResourceKey.Should().Be("GeneratedSheetNameInvalid");
    }

    [Fact]
    public void Validate_WithTwoNamesEqualIgnoringCase_ThrowsGeneratedSheetNameConflict()
    {
        var act = () => GeneratedSheetNameValidator.Validate(["Parents", "Enfants", "parents"]);

        var exception = act.Should().Throw<GeneratedSheetNameException>().Which;
        exception.ErrorCode.Should().Be(ApplicationErrorCode.GeneratedSheetNameConflict);
        exception.SheetName.Should().Be("parents");
        exception.Args.Should().Equal("parents");
    }
}
