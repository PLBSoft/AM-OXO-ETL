using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.5 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportProfileDescriptionBuilderPointTests
{
    private static readonly ColumnDefinition RepereColumn = new("Repère", PivotFieldRef.EquipementRepere);
    private static readonly ColumnDefinition NumeroColumn = new("Numéro", PivotFieldRef.IsolementRepere);

    private static PointColumnDefinition Point(string name, string markValue = "X") => new(name, name, markValue);

    private static IReadOnlyList<string> ColumnTexts(SheetGenerationRule rule) =>
        Describe(ExportProfile(rule)).Sections[1].Texts().Skip(1).ToList();

    [Fact]
    public void Parents_ApplicationThenGroupedPoints_WithTheToleranceNote()
    {
        var rule = ExportRule("Parents", PivotSource.Equipement, [RepereColumn],
            pointColumns: [Point("PROLOCK VANNES"), Point("DEPROLOCK VANNES"), Point("POSE ÉTIQUETTES")],
            applicationColumns: [new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")]);

        ColumnTexts(rule).Should().Equal(
            "Colonne A « Repère » : le repère de l'équipement.",
            "Colonne B « PROGRESS » : « O » si l'équipement est rattaché à l'application « PROGRESS », sinon vide.",
            "Colonnes C à E : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « PROLOCK VANNES » (C), « DEPROLOCK VANNES » (D), « POSE ÉTIQUETTES » (E).");
    }

    // Lot 081 (docs/tickets/tickets-tdd-lot-081-comparaison-noms-colonne-points-export.md, 81.4):
    // renamed from "..._WithTheExactNameNote" -- the moteur is now tolerant on both sheets (D1), so the
    // note describes the real, shared comparison rule instead of claiming an exact match.
    [Fact]
    public void Enfants_ApplicationThenGroupedPoints_WithTheToleranceNote()
    {
        var rule = ExportRule("Enfants", PivotSource.Isolement, [NumeroColumn],
            pointColumns: [Point("PROLOCK VANNES"), Point("DEPROLOCK VANNES"), Point("POSE ÉTIQUETTES")],
            applicationColumns: [new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")]);

        ColumnTexts(rule).Skip(1).Should().Equal(
            "Colonne B « PROGRESS » : « O » si l'élément est rattaché à l'application « PROGRESS », sinon vide.",
            "Colonnes C à E : « X » si l'élément est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « PROLOCK VANNES » (C), « DEPROLOCK VANNES » (D), « POSE ÉTIQUETTES » (E).");
    }

    [Fact]
    public void TwoMarkValues_GiveTwoGroups()
    {
        var rule = ExportRule("Parents", PivotSource.Equipement,
            pointColumns: [Point("A1"), Point("A2"), Point("B1", "O"), Point("B2", "O")]);

        ColumnTexts(rule).Should().Equal(
            "Colonnes A à B : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « A1 » (A), « A2 » (B).",
            "Colonnes C à D : « O » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « B1 » (C), « B2 » (D).");
    }

    [Fact]
    public void PointWhoseHeaderDiffersFromTheColonneName_HasItsOwnSentence()
    {
        var rule = ExportRule("Enfants", PivotSource.Isolement,
            pointColumns: [new PointColumnDefinition("ZÉRO ENERGIE EN PRESENCE EE (PS941)", "PS941", "X")]);

        ColumnTexts(rule).Should().Equal(
            "Colonne A « PS941 » : « X » si l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) » à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide.");
    }

    [Fact]
    public void GroupOfASingleColumn_IsDescribedAlone()
    {
        var rule = ExportRule("Parents", PivotSource.Equipement,
            pointColumns: [Point("PROLOCK VANNES"), Point("POSE ÉTIQUETTES", "O")]);

        ColumnTexts(rule).Should().Equal(
            "Colonne A « PROLOCK VANNES » : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne « PROLOCK VANNES » à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide.",
            "Colonne B « POSE ÉTIQUETTES » : « O » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne « POSE ÉTIQUETTES » à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide.");
    }

    [Fact]
    public void NonConsecutiveGroup_ListsItsLetters_AndSitsAtItsFirstColumn()
    {
        var rule = ExportRule("Parents", PivotSource.Equipement,
            pointColumns: [Point("A1"), Point("B1", "O"), Point("A2"), Point("A3")]);

        ColumnTexts(rule).Should().Equal(
            "Colonnes A, C et D : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide : « A1 » (A), « A2 » (C), « A3 » (D).",
            "Colonne B « B1 » : « O » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne « B1 » à l'import (sans tenir compte des majuscules ni des espaces en début ou fin), sinon vide.");
    }
}
