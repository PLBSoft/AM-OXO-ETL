using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class ExportProfileTests
{
    private static SheetGenerationRule ValidRule(string sheetName = "Parents") => new(
        sheetName,
        PivotSource.Equipement,
        [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
        [],
        []);

    [Fact]
    public void Constructor_WithValidArguments_CreatesExportProfile()
    {
        var rule = ValidRule();

        var profile = new ExportProfile("Profil export OXO standard", [rule]);

        profile.Name.Should().Be("Profil export OXO standard");
        profile.SheetRules.Should().BeEquivalentTo([rule]);
        profile.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new ExportProfile(invalidName!, [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExportProfile_EmptyName);
    }

    [Fact]
    public void Constructor_WithNullSheetRules_ThrowsArgumentNullException()
    {
        var act = () => new ExportProfile("Profil export OXO standard", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNoSheetRules_ThrowsDomainValidationException()
    {
        var act = () => new ExportProfile("Profil export OXO standard", []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetRules")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExportProfile_NoSheetRules);
    }

    [Fact]
    public void Constructor_WithMultipleSheetRules_CreatesExportProfile()
    {
        var profile = new ExportProfile("Profil export OXO standard", [ValidRule("Parents"), ValidRule("Enfants")]);

        profile.SheetRules.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithExplicitId_ReconstructsProfileUnderThatId()
    {
        var existingId = Guid.NewGuid();

        var profile = new ExportProfile(existingId, "Profil export OXO standard (édité)", [ValidRule()]);

        profile.Id.Should().Be(existingId);
        profile.Name.Should().Be("Profil export OXO standard (édité)");
    }

    [Fact]
    public void Constructor_WithEmptyExplicitId_ThrowsArgumentException()
    {
        var act = () => new ExportProfile(Guid.Empty, "Profil export OXO standard", [ValidRule()]);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var first = new ExportProfile(id, "Profil export OXO standard", [ValidRule()]);
        var second = new ExportProfile(id, "Profil export OXO standard", [ValidRule()]);

        first.Should().Be(second);
    }

    [Fact]
    public void Equality_WithDifferentId_AreNotEqual()
    {
        var first = new ExportProfile("Profil export OXO standard", [ValidRule()]);
        var second = new ExportProfile("Profil export OXO standard", [ValidRule()]);

        first.Should().NotBe(second);
    }
}
