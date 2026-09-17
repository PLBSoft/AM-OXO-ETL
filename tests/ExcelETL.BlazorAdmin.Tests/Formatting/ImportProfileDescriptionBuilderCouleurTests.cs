using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.6 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportProfileDescriptionBuilderCouleurTests
{
    private static readonly BlockFieldDefinition CouleurCell = new("CouleurEtiquette", "H:N", 1, 1);

    [Fact]
    public void Platines_CellAndAllowedColours() =>
        Describe(Profile([Rule("PLATINES", firstBlockStartRow: 17, couleurEtiquetteCell: CouleurCell,
                allowedCouleursEtiquette: ["ROUGE", "BLANC", "JAUNE", "VERT", "BLEUE"])]))
            .SheetSection("PLATINES").Texts().Should().EndWith(
                "La couleur d'étiquette est lue en H18:N18. Couleurs acceptées : « ROUGE », « BLANC », « JAUNE », « VERT », « BLEUE ». " +
                "Une autre valeur est ignorée, avec un avertissement.");

    [Fact]
    public void CellWithoutAllowedColours_AcceptsAnyValue() =>
        Describe(Profile([Rule("ORIFICES CAPACITES", firstBlockStartRow: 17, couleurEtiquetteCell: CouleurCell)]))
            .SheetSection("ORIFICES CAPACITES").Texts().Should().EndWith(
                "La couleur d'étiquette est lue en H18:N18. Toute valeur est acceptée.");

    [Fact]
    public void AutresJointsTouches_DefaultColourOnly() =>
        Describe(Profile([Rule("AUTRES JOINTS TOUCHES", defaultCouleurEtiquette: "BLEUE")]))
            .SheetSection("AUTRES JOINTS TOUCHES").Texts().Should().EndWith(
                "La couleur d'étiquette de chaque élément est toujours « BLEUE ».");

    [Fact]
    public void CellAndDefault_OnlyTheCellIsDescribed_SinceTheDefaultIsNeverUsed()
    {
        var texts = Describe(Profile([Rule("PLATINES", firstBlockStartRow: 17, couleurEtiquetteCell: CouleurCell,
            defaultCouleurEtiquette: "BLEUE")])).SheetSection("PLATINES").Texts();

        texts.Should().EndWith("La couleur d'étiquette est lue en H18:N18. Toute valeur est acceptée.");
        texts.Should().NotContain(t => t.Contains("toujours"));
    }

    [Fact]
    public void AllowedColoursWithoutCell_AreNotDescribed() =>
        Describe(Profile([Rule("PLATINES", allowedCouleursEtiquette: ["ROUGE"])]))
            .SheetSection("PLATINES").Texts().Should().NotContain(t => t.Contains("couleur"));

    [Fact]
    public void NothingSet_NoColourSentence() =>
        Describe(Profile([Rule("PLATINES")])).SheetSection("PLATINES").Texts().Should().NotContain(t => t.Contains("couleur"));

    [Fact]
    public void SheetThatDoesNotReadColours_DescribesNone() =>
        Describe(Profile([Rule("DIVERS", defaultCouleurEtiquette: "VERT", couleurEtiquetteCell: CouleurCell)]))
            .SheetSection("DIVERS").Texts().Should().NotContain(t => t.Contains("couleur"));
}
