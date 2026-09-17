using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

// Lot 081 (docs/tickets/tickets-tdd-lot-081-comparaison-noms-colonne-points-export.md, 81.1): the single
// comparison rule for a "Colonne"/"Application" name, shared by SheetGenerationEngine (Application) and
// SheetGenerationRule's own uniqueness checks (Domain) -- Trim then OrdinalIgnoreCase, no diacritics
// normalization (spec §7, Lot 055's own guard-rail: a genuine spelling difference stays a real mismatch).
public class ColonneNameComparerTests
{
    [Theory]
    [InlineData("ZÉRO ENERGIE", "zéro energie")]
    [InlineData("ZÉRO ENERGIE", " ZÉRO ENERGIE ")]
    public void Equals_TrimmedAndCaseInsensitiveVariants_AreEqual(string a, string b)
    {
        ColonneNameComparer.Instance.Equals(a, b).Should().BeTrue();
        ColonneNameComparer.Instance.GetHashCode(a).Should().Be(ColonneNameComparer.Instance.GetHashCode(b));
    }

    [Fact]
    public void Equals_DifferingOnlyByDiacritics_AreNotEqual() =>
        ColonneNameComparer.Instance.Equals("ZÉRO ENERGIE", "ZERO ENERGIE").Should().BeFalse();

    [Fact]
    public void Equals_GenuineSpellingDifference_AreNotEqual() =>
        // Same guard-rail as Lot 055's ConditionalPointGroupEvaluator: "POINT DE FEU" vs "POINT FEU"
        // is a real, non-normalized mismatch, not a formatting variance.
        ColonneNameComparer.Instance.Equals("POINT DE FEU", "POINT FEU").Should().BeFalse();

    [Fact]
    public void Equals_BothNull_AreEqual() =>
        ColonneNameComparer.Instance.Equals(null, null).Should().BeTrue();

    [Fact]
    public void Equals_NullAndAString_AreNotEqual() =>
        ColonneNameComparer.Instance.Equals(null, "ZÉRO ENERGIE").Should().BeFalse();

    [Fact]
    public void Instance_IsAStringComparer() =>
        ColonneNameComparer.Instance.Should().BeAssignableTo<StringComparer>();
}
