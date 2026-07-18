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

    [Fact]
    public void Constructor_WithValidArguments_CreatesSheetGenerationRule()
    {
        var columns = ValidColumns();
        var points = ValidPoints();

        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, columns, points);

        rule.SheetName.Should().Be("Parents");
        rule.PivotSource.Should().Be(PivotSource.Equipement);
        rule.ColumnDefinitions.Should().BeEquivalentTo(columns);
        rule.PointColumnDefinitions.Should().BeEquivalentTo(points);
    }

    [Fact]
    public void Constructor_WithEmptyColumnDefinitions_CreatesSheetGenerationRule()
    {
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, [], ValidPoints());

        rule.ColumnDefinitions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyPointColumnDefinitions_CreatesSheetGenerationRule()
    {
        var rule = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), []);

        rule.PointColumnDefinitions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheetName_ThrowsDomainValidationException(string? invalidSheetName)
    {
        var act = () => new SheetGenerationRule(invalidSheetName!, PivotSource.Equipement, ValidColumns(), ValidPoints());

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_EmptySheetName);
    }

    [Fact]
    public void Constructor_WithNullColumnDefinitions_ThrowsArgumentNullException()
    {
        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, null!, ValidPoints());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPointColumnDefinitions_ThrowsArgumentNullException()
    {
        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithColumnSourceIncompatibleWithPivotSource_ThrowsDomainRuleViolationExceptionAtConstructionTime()
    {
        // IsolementPositionALaPose has no meaning on an Equipement row -- this must be rejected when
        // the profile is built, not silently ignored (or thrown) later when a file is generated.
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Position MAD", PivotFieldRef.IsolementPositionALaPose)];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_ColumnSourceIncompatibleWithPivotSource);
    }

    [Fact]
    public void Constructor_WithColumnSourceCompatibleWithPivotSource_CreatesSheetGenerationRule()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Position MAD", PivotFieldRef.IsolementPositionALaPose)];

        var rule = new SheetGenerationRule("Enfants", PivotSource.Isolement, columns, []);

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

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, []);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateHeader);
    }

    [Fact]
    public void Constructor_WithHeaderCollisionBetweenColumnAndPointDefinitions_ThrowsDomainValidationException()
    {
        IReadOnlyList<ColumnDefinition> columns = [new ColumnDefinition("Prolock vannes", null)];
        IReadOnlyList<PointColumnDefinition> points = [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")];

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, columns, points);

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

        var act = () => new SheetGenerationRule("Parents", PivotSource.Equipement, [], points);

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetGenerationRule_DuplicateColonneNom);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints());
        var second = new SheetGenerationRule("Parents", PivotSource.Equipement, ValidColumns(), ValidPoints());

        first.Should().Be(second);
    }
}
