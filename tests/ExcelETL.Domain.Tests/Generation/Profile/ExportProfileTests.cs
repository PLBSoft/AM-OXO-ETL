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

    [Fact]
    public void Constructor_WithNameOfExactly60Characters_CreatesExportProfile()
    {
        var name = new string('A', 60);

        var profile = new ExportProfile(name, [ValidRule()]);

        profile.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithNameOf61Characters_ThrowsDomainValidationException()
    {
        var name = new string('A', 61);

        var act = () => new ExportProfile(name, [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExportProfile_NameTooLong);
    }

    [Fact]
    public void Constructor_WithNameOf65CharactersTrimmingTo60_CreatesExportProfile()
    {
        var name = " " + new string('A', 60) + "    ";

        var profile = new ExportProfile(name, [ValidRule()]);

        profile.Name.Should().Be(name);
    }

    // Lot 080.3 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md): checks between the
    // profile's rules that make ClosedXmlWorkbookWriter fail.
    private static SheetGenerationRule IsolementRule(string sheetName) => new(
        sheetName, PivotSource.Isolement, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)], [], []);

    private static SheetGenerationRule TacheMultipleRule(string sheetName = "Tâches multiples") => new(
        sheetName, PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)], [], []);

    [Fact]
    public void Constructor_WithTwoSheetNamesEqualIgnoringCase_ThrowsDuplicateSheetName()
    {
        var act = () => new ExportProfile("Profil export OXO standard", [ValidRule("Parents"), IsolementRule("parents")]);

        var exception = act.Should().Throw<DomainValidationException>().WithParameterName("sheetRules").Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.ExportProfile_DuplicateSheetName);
        exception.Args.Should().ContainSingle().Which.Should().Be("parents");
    }

    [Fact]
    public void Constructor_WithSheetNamesDifferingOnlyBySurroundingSpaces_IsAccepted()
    {
        var profile = new ExportProfile("Profil export OXO standard", [ValidRule(" Parents "), IsolementRule("Parents")]);

        profile.SheetRules.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithTwoTacheMultipleRules_ThrowsSeveralTacheMultipleRules()
    {
        var act = () => new ExportProfile(
            "Profil export OXO standard", [ValidRule(), TacheMultipleRule("Tâches A"), TacheMultipleRule("Tâches B")]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetRules")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExportProfile_SeveralTacheMultipleRules);
    }

    // Non-generalization: a TacheMultiple rule's label is never a sheet name, so it can't clash with one.
    [Fact]
    public void Constructor_WithTacheMultipleLabelEqualToASheetName_IsAccepted()
    {
        var profile = new ExportProfile("Profil export OXO standard", [ValidRule("Parents"), TacheMultipleRule("Parents")]);

        profile.SheetRules.Should().HaveCount(2);
    }
}
