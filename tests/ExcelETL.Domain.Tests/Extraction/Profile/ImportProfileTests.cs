using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class ImportProfileTests
{
    private const string EquipementTypeElementNom = "MAD TRAVAUX";

    private static SheetExtractionRule ValidRule(string sheet = "ISOLEMENT") => new(
        sheet,
        new RepeatingBlockLocator(sheet, 19, 7, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]),
        [],
        []);

    [Fact]
    public void Constructor_WithValidArguments_CreatesImportProfile()
    {
        var rule = ValidRule();

        var profile = new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [rule]);

        profile.Name.Should().Be("Profil OXO standard");
        profile.ReperePrefix.Should().Be("MAD-OXO-");
        profile.EquipementTypeElementNom.Should().Be(EquipementTypeElementNom);
        profile.SheetRules.Should().BeEquivalentTo([rule]);
        profile.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithoutReperePrefix_DefaultsToMadOxo()
    {
        var profile = new ImportProfile("Profil OXO standard", EquipementTypeElementNom, sheetRules: [ValidRule()]);

        profile.ReperePrefix.Should().Be("MAD-OXO-");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new ImportProfile(invalidName!, "MAD-OXO-", EquipementTypeElementNom, [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidReperePrefix_ThrowsDomainValidationException(string? invalidReperePrefix)
    {
        var act = () => new ImportProfile("Profil OXO standard", invalidReperePrefix!, EquipementTypeElementNom, [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("reperePrefix")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyReperePrefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidEquipementTypeElementNom_ThrowsDomainValidationException(string? invalidValue)
    {
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", invalidValue!, [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("equipementTypeElementNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyEquipementTypeElementNom);
    }

    [Fact]
    public void Constructor_WithNullSheetRules_ThrowsArgumentNullException()
    {
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNoSheetRules_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetRules")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_NoSheetRules);
    }

    [Fact]
    public void Constructor_WithMultipleSheetRules_CreatesImportProfile()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [ValidRule("ISOLEMENT"), ValidRule("PLATINES")]);

        profile.SheetRules.Should().HaveCount(2);
    }
}
