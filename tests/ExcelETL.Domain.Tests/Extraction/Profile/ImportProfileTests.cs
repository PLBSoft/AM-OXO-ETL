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
        [],
        [],
        []);

    [Fact]
    public void Constructor_WithValidArguments_CreatesImportProfile()
    {
        var rule = ValidRule();

        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom,
            ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], ["PROGRESS"], [rule]);

        profile.Name.Should().Be("Profil OXO standard");
        profile.ReperePrefix.Should().Be("MAD-OXO-");
        profile.EquipementTypeElementNom.Should().Be(EquipementTypeElementNom);
        profile.DefaultTableaux.Should().BeEquivalentTo(["TRAVAUX COMPLET", "TRAVAUX DETAIL"], o => o.WithStrictOrdering());
        profile.DefaultApplicationNames.Should().BeEquivalentTo(["PROGRESS"]);
        profile.SheetRules.Should().BeEquivalentTo([rule]);
        profile.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithoutReperePrefix_DefaultsToMadOxo()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", EquipementTypeElementNom, [], [], sheetRules: [ValidRule()]);

        profile.ReperePrefix.Should().Be("MAD-OXO-");
    }

    [Fact]
    public void Constructor_WithEmptyTableauxAndApplicationNames_CreatesImportProfile()
    {
        var profile = new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        profile.DefaultTableaux.Should().BeEmpty();
        profile.DefaultApplicationNames.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullDefaultTableaux_ThrowsArgumentNullException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, null!, [], [ValidRule()]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDefaultApplicationNames_ThrowsArgumentNullException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], null!, [ValidRule()]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new ImportProfile(invalidName!, "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

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
        var act = () => new ImportProfile(
            "Profil OXO standard", invalidReperePrefix!, EquipementTypeElementNom, [], [], [ValidRule()]);

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
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", invalidValue!, [], [], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("equipementTypeElementNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyEquipementTypeElementNom);
    }

    [Fact]
    public void Constructor_WithNullSheetRules_ThrowsArgumentNullException()
    {
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNoSheetRules_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile("Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetRules")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_NoSheetRules);
    }

    [Fact]
    public void Constructor_WithMultipleSheetRules_CreatesImportProfile()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [],
            [ValidRule("ISOLEMENT"), ValidRule("PLATINES")]);

        profile.SheetRules.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithExplicitId_ReconstructsProfileUnderThatId()
    {
        var existingId = Guid.NewGuid();

        var profile = new ImportProfile(
            existingId, "Profil OXO standard (edite)", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        profile.Id.Should().Be(existingId);
        profile.Name.Should().Be("Profil OXO standard (edite)");
    }

    [Fact]
    public void Constructor_WithEmptyExplicitId_ThrowsArgumentException()
    {
        var act = () => new ImportProfile(
            Guid.Empty, "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Constructor_WithNameOfExactly60Characters_CreatesImportProfile()
    {
        var name = new string('A', 60);

        var profile = new ImportProfile(name, "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        profile.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithNameOf61Characters_ThrowsDomainValidationException()
    {
        var name = new string('A', 61);

        var act = () => new ImportProfile(name, "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_NameTooLong);
    }

    [Fact]
    public void Constructor_WithNameOf65CharactersTrimmingTo60_CreatesImportProfile()
    {
        var name = " " + new string('A', 60) + "    ";

        var profile = new ImportProfile(name, "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        profile.Name.Should().Be(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyTableauName_ThrowsDomainValidationException(string invalidName)
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [invalidName], [], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyTableauName);
    }

    [Fact]
    public void Constructor_WithTableauNameOfExactly50Characters_CreatesImportProfile()
    {
        var name = new string('A', 50);

        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [name], [], [ValidRule()]);

        profile.DefaultTableaux.Should().BeEquivalentTo([name]);
    }

    [Fact]
    public void Constructor_WithTableauNameOf51Characters_ThrowsDomainValidationException()
    {
        var name = new string('A', 51);

        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [name], [], [ValidRule()]);

        var exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_TableauNameTooLong);
        exception.Args.Should().ContainSingle().Which.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithTableauNameOf55CharactersTrimmingTo50_CreatesImportProfile()
    {
        var name = "  " + new string('A', 50) + "   ";

        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [name], [], [ValidRule()]);

        profile.DefaultTableaux.Should().BeEquivalentTo([new string('A', 50)]);
    }

    [Fact]
    public void Constructor_WithCaseInsensitiveDuplicateTableauNames_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, ["zzz", "ZZZ"], [], [ValidRule()]);

        var exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateTableauName);
        exception.Args.Should().ContainSingle().Which.Should().Be("ZZZ");
    }

    [Fact]
    public void Constructor_WithDuplicateTableauNamesDifferingOnlyByWhitespace_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, ["zzz", " zzz "], [], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateTableauName);
    }

    [Fact]
    public void Constructor_WithTableauNameSurroundedByWhitespace_StoresTrimmedValue()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, ["  zzz  "], [], [ValidRule()]);

        profile.DefaultTableaux.Should().BeEquivalentTo(["zzz"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyApplicationName_ThrowsDomainValidationException(string invalidName)
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [invalidName], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_EmptyApplicationName);
    }

    [Fact]
    public void Constructor_WithApplicationNameOfExactly50Characters_CreatesImportProfile()
    {
        var name = new string('A', 50);

        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [name], [ValidRule()]);

        profile.DefaultApplicationNames.Should().BeEquivalentTo([name]);
    }

    [Fact]
    public void Constructor_WithApplicationNameOf51Characters_ThrowsDomainValidationException()
    {
        var name = new string('A', 51);

        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [name], [ValidRule()]);

        var exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_ApplicationNameTooLong);
        exception.Args.Should().ContainSingle().Which.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithApplicationNameOf55CharactersTrimmingTo50_CreatesImportProfile()
    {
        var name = "  " + new string('A', 50) + "   ";

        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [name], [ValidRule()]);

        profile.DefaultApplicationNames.Should().BeEquivalentTo([new string('A', 50)]);
    }

    [Fact]
    public void Constructor_WithCaseInsensitiveDuplicateApplicationNames_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], ["PROGRESS", "progress"], [ValidRule()]);

        var exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateApplicationName);
        exception.Args.Should().ContainSingle().Which.Should().Be("progress");
    }

    [Fact]
    public void Constructor_WithDuplicateApplicationNamesDifferingOnlyByWhitespace_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], ["PROGRESS", " PROGRESS "], [ValidRule()]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateApplicationName);
    }

    [Fact]
    public void Constructor_WithApplicationNameSurroundedByWhitespace_StoresTrimmedValue()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], ["  PROGRESS  "], [ValidRule()]);

        profile.DefaultApplicationNames.Should().BeEquivalentTo(["PROGRESS"]);
    }

    [Fact]
    public void Constructor_WithSameNameInBothTableauxAndApplicationNames_CreatesImportProfile()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, ["SHARED"], ["SHARED"], [ValidRule()]);

        profile.DefaultTableaux.Should().BeEquivalentTo(["SHARED"]);
        profile.DefaultApplicationNames.Should().BeEquivalentTo(["SHARED"]);
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md):
    // TacheMultipleTypeLabels is a deliberately optional, last-position parameter (decision 5) --
    // omitting it must keep every pre-existing call site (like every test above) working unchanged.
    [Fact]
    public void Constructor_WithoutTacheMultipleTypeLabels_DefaultsToEmptyList()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()]);

        profile.TacheMultipleTypeLabels.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithTacheMultipleTypeLabels_StoresThemTrimmed()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()],
            [new TacheMultipleTypeLabel(" TM_PROC_MAD ", " Procédure MAD ")]);

        profile.TacheMultipleTypeLabels.Should().ContainSingle();
        profile.TacheMultipleTypeLabels[0].Code.Should().Be("TM_PROC_MAD");
        profile.TacheMultipleTypeLabels[0].Label.Should().Be("Procédure MAD");
    }

    [Fact]
    public void Constructor_WithDuplicateTacheMultipleTypeLabelCode_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()],
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"), new TacheMultipleTypeLabel("TM_PROC_MAD", "Autre libellé")]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateTacheMultipleTypeLabelCode);
    }

    [Fact]
    public void Constructor_WithDuplicateTacheMultipleTypeLabelCodeDifferingOnlyByWhitespaceAndCase_ThrowsDomainValidationException()
    {
        var act = () => new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()],
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"), new TacheMultipleTypeLabel(" tm_proc_mad ", "Autre libellé")]);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ImportProfile_DuplicateTacheMultipleTypeLabelCode);
    }

    [Fact]
    public void Constructor_WithDifferentCodesSharingTheSameLabel_CreatesImportProfile()
    {
        var profile = new ImportProfile(
            "Profil OXO standard", "MAD-OXO-", EquipementTypeElementNom, [], [], [ValidRule()],
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure"), new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure")]);

        profile.TacheMultipleTypeLabels.Should().HaveCount(2);
    }
}
