using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class SheetGenerationRuleTests
{
    private static IReadOnlyList<ColumnDefinition> ValidColumns() =>
    [
        new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
        new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation)
    ];

    private static IReadOnlyList<PointColumnDefinition> ValidPoints() =>
    [
        new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
        new PointColumnDefinition("DEPROLOCK VANNES", "Deprolock vannes")
    ];

    private static IReadOnlyList<ApplicationColumnDefinition> ValidApplications() =>
    [
        new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")
    ];

    [Fact]
    public void Constructor_WithValidArguments_CreatesSheetGenerationRule()
    {
        var columns = ValidColumns();
        var points = ValidPoints();

        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, columns, points, []);

        rule.SheetName.Should().Be("Parents");
        rule.PivotSource.Should().Be(PivotSource.Equipement);
        rule.ColumnDefinitions.Should().BeEquivalentTo(columns);
        rule.PointColumnDefinitions.Should().BeEquivalentTo(points);
        rule.ApplicationColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyColumnDefinitions_CreatesSheetGenerationRule()
    {
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, [], ValidPoints(), []);

        rule.ColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyPointColumnDefinitions_CreatesSheetGenerationRule()
    {
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), [], []);

        rule.PointColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithApplicationColumnDefinitions_CreatesSheetGenerationRule()
    {
        var applications = ValidApplications();

        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), applications);

        rule.ApplicationColumnDefinitions.Should().BeEquivalentTo(applications);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheetName_ThrowsDomainValidationException(string? invalidSheetName)
    {
        var act = () => new SheetGenerationRule(invalidSheetName!, PivotSource.Equipement, ValidColumns(), ValidPoints(), []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_EmptySheetName);
    }

    [Fact]
    public void Constructor_WithNullColumnDefinitions_ThrowsArgumentNullException()
    {
        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, null!, ValidPoints(), []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPointColumnDefinitions_ThrowsArgumentNullException()
    {
        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullApplicationColumnDefinitions_ThrowsArgumentNullException()
    {
        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithColumnSourceIncompatibleWithPivotSource_ThrowsDomainRuleViolationExceptionAtConstructionTime()
    {
        // IsolementPositionALaPose has no meaning on an Equipement row -- this must be rejected when
        // the profile is built, not silently ignored (or thrown) later when a file is generated.
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Position MAD", PivotFieldRef.IsolementPositionALaPose)];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, [], []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_ColumnSourceIncompatibleWithPivotSource);
    }

    [Fact]
    public void Constructor_WithColumnSourceCompatibleWithPivotSource_CreatesSheetGenerationRule()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Position MAD", PivotFieldRef.IsolementPositionALaPose)];

        var rule = new SheetGenerationRule("Enfants", PivotSource.Isolement, columns, [], []);

        rule.ColumnDefinitions.Should().BeEquivalentTo(columns);
    }

    [Fact]
    public void Constructor_WithDuplicateHeaderAmongColumnDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ColumnDefinition> columns =
        [
            new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
            new ColumnDefinition("Repère", PivotFieldRef.EquipementDesignation)
        ];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, [], []);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateHeader);
    }

    [Fact]
    public void Constructor_WithHeaderCollisionBetweenColumnAndPointDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Prolock vannes", null)];
        IReadOnlyList<PointColumnDefinition> points = [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, points, []);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateHeader);
    }

    [Fact]
    public void Constructor_WithHeaderCollisionBetweenColumnAndApplicationDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("PROGRESS", null)];
        IReadOnlyList<ApplicationColumnDefinition> applications = [new ApplicationColumnDefinition("PROGRESS", "PROGRESS")];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, [], applications);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateHeader);
    }

    [Fact]
    public void Constructor_WithDuplicateColonneNomAmongPointColumnDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<PointColumnDefinition> points =
        [
            new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
            new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes (bis)")
        ];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, [], points, []);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateColonneNom);
    }

    [Fact]
    public void Constructor_WithDuplicateApplicationNomAmongApplicationColumnDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ApplicationColumnDefinition> applications =
        [
            new ApplicationColumnDefinition("PROGRESS", "PROGRESS"),
            new ApplicationColumnDefinition("PROGRESS", "PROGRESS (bis)")
        ];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, [], [], applications);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateApplicationNom);
    }

    [Fact]
    public void Constructor_WithTacheMultiplePivotSourceAndEquipementColumnSource_ThrowsDomainRuleViolationException()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)];

        var act = () => new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, columns, [], []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_ColumnSourceIncompatibleWithPivotSource);
    }

    [Fact]
    public void Constructor_WithTacheMultiplePivotSourceAndPointColumnDefinitions_ThrowsDomainRuleViolationException()
    {
        IReadOnlyList<PointColumnDefinition> points = [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")];

        var act = () => new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, [], points, []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_PointColumnDefinitionsNotAllowedForTacheMultiple);
    }

    [Fact]
    public void Constructor_WithTacheMultiplePivotSourceAndApplicationColumnDefinitions_ThrowsDomainRuleViolationException()
    {
        IReadOnlyList<ApplicationColumnDefinition> applications = [new ApplicationColumnDefinition("PROGRESS", "PROGRESS")];

        var act = () => new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, [], [], applications);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_ApplicationColumnDefinitionsNotAllowedForTacheMultiple);
    }

    [Fact]
    public void Constructor_WithTacheMultiplePivotSourceAndTacheMultipleColumnSource_CreatesSheetGenerationRule()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction)];

        var rule = new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, columns, [], []);

        rule.PivotSource.Should().Be(PivotSource.TacheMultiple);
        rule.ColumnDefinitions.Should().BeEquivalentTo(columns);
        rule.PointColumnDefinitions.Should().BeEmpty();
        rule.ApplicationColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEquipementOrIsolementPivotSource_IsUnaffectedByTacheMultipleValidations()
    {
        var equipementRule = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), ValidApplications());
        var isolementRule = new SheetGenerationRule(
            "Enfants", PivotSource.Isolement,
            [new ColumnDefinition("Repère", PivotFieldRef.IsolementRepere)], ValidPoints(), ValidApplications());

        equipementRule.PointColumnDefinitions.Should().BeEquivalentTo(ValidPoints());
        equipementRule.ApplicationColumnDefinitions.Should().BeEquivalentTo(ValidApplications());
        isolementRule.PointColumnDefinitions.Should().BeEquivalentTo(ValidPoints());
        isolementRule.ApplicationColumnDefinitions.Should().BeEquivalentTo(ValidApplications());
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), ValidApplications());
        var second = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), ValidApplications());

        first.Should().Be(second);
    }

    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
    // ConstantColumnDefinitions is the 4th, optional collection -- omitted by every test above, which
    // must all still pass unmodified (confirmed by the full non-regression run before adding these).
    [Fact]
    public void Constructor_WithConstantColumnDefinitionsOmitted_DefaultsToEmpty()
    {
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), []);

        rule.ConstantColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithConstantColumnDefinitions_CreatesSheetGenerationRule()
    {
        IReadOnlyList<ConstantColumnDefinition> constants = [new ConstantColumnDefinition("CRITERE", "A faire")];

        var rule = new SheetGenerationRule(
            "Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), [], constants);

        rule.ConstantColumnDefinitions.Should().BeEquivalentTo(constants);
    }

    [Fact]
    public void Constructor_WithHeaderCollisionBetweenColumnAndConstantDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("CRITERE", null)];
        IReadOnlyList<ConstantColumnDefinition> constants = [new ConstantColumnDefinition("CRITERE", "A faire")];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, [], [], constants);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateHeader);
    }

    [Fact]
    public void Constructor_WithTacheMultiplePivotSourceAndConstantColumnDefinitions_CreatesSheetGenerationRule()
    {
        // Unlike Point/Application columns, a constant column references no pivot field at all -- it is
        // explicitly allowed for PivotSource.TacheMultiple.
        IReadOnlyList<ConstantColumnDefinition> constants = [new ConstantColumnDefinition("CRITERE", "A faire")];

        var rule = new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple, [], [], [], constants);

        rule.ConstantColumnDefinitions.Should().BeEquivalentTo(constants);
    }

    [Fact]
    public void Equality_WithSameConstantColumnDefinitions_AreEqual()
    {
        IReadOnlyList<ConstantColumnDefinition> constants = [new ConstantColumnDefinition("CRITERE", "A faire")];

        var first = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), [], constants);
        var second = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), [], constants);

        first.Should().Be(second);
    }

    [Fact]
    public void Equality_WithDifferentConstantColumnDefinitions_AreNotEqual()
    {
        var first = new SheetGenerationRule(
            "Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), [],
            [new ConstantColumnDefinition("CRITERE", "A faire")]);
        var second = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints(), []);

        first.Should().NotBe(second);
    }
}
