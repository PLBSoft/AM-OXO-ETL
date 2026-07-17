using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class ImportResultTests
{
    [Fact]
    public void Constructor_WithNoErrors_CreatesSuccessfulResult()
    {
        var equipement = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD");
        var isolements = new List<IsolementPivot> { new("C7401-ISO1", "Vanne", "ZERO ENERGIE", "FERMÉE", "Zone A") };
        var points = new List<PointPivot> { new("PROLOCK VANNES", "C7401-ISO1") };
        var taches = new List<TacheMultiplePivot> { new(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", null, false) };

        var result = new ImportResult(equipement, isolements, points, taches, []);

        result.Equipement.Should().Be(equipement);
        result.Isolements.Should().BeEquivalentTo(isolements);
        result.Points.Should().BeEquivalentTo(points);
        result.TachesMultiples.Should().BeEquivalentTo(taches);
        result.Errors.Should().BeEmpty();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithErrors_HasErrorsIsTrue()
    {
        var error = new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "Cellule M2:O2 introuvable ou vide.");

        var result = new ImportResult(null, [], [], [], [error]);

        result.Equipement.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Should().Be(error);
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullCollections_ThrowsArgumentNullException()
    {
        var act = () => new ImportResult(null, null!, [], [], []);

        act.Should().Throw<ArgumentNullException>();
    }
}
