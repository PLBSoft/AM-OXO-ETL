using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class NoConditionalPointCreatedWarningTrackerTests
{
    private const string Sheet = "ISOLEMENT";

    [Fact]
    public void RecordIfNew_FirstOccurrence_AddsStructuredErrorWithFrenchMessage()
    {
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("C7401-V1", "PROLOCK", NullLogger.Instance, errors);

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(ExtractionErrorCode.NoConditionalPointCreated);
        error.Sheet.Should().Be(Sheet);
        error.BlockIdentifier.Should().Be("C7401-V1");
        error.ExtractedValue.Should().Be("PROLOCK");
        error.Message.Should().Be(
            "Aucun Point conditionnel n'a été créé pour la valeur « PROLOCK » : aucune condition " +
            "du profil d'import ne correspond à cette valeur pour cette feuille.");
        error.Message.Should().NotContain("Colonne");
    }

    [Fact]
    public void RecordIfNew_EightIdenticalValues_ProducesExactlyOneEntry()
    {
        // 55.5 case a.
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        for (var i = 0; i < 8; i++)
        {
            tracker.RecordIfNew($"C7401-V{i}", "PROLOCK", NullLogger.Instance, errors);
        }

        errors.Should().ContainSingle().Which.ExtractedValue.Should().Be("PROLOCK");
    }

    [Fact]
    public void RecordIfNew_TwoDistinctValues_ProducesOneEntryPerValue()
    {
        // 55.5 case b.
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("C7401-V1", "PROLOCK", NullLogger.Instance, errors);
        tracker.RecordIfNew("D8570-V4", "VANNE", NullLogger.Instance, errors);

        errors.Should().HaveCount(2);
        errors.Select(e => e.ExtractedValue).Should().BeEquivalentTo(["PROLOCK", "VANNE"]);
    }

    [Fact]
    public void RecordIfNew_TrimAndCaseInsensitiveDuplicate_KeepsFirstRawFormOnly()
    {
        // 55.5 case c: consistent with ConditionalPointRuleEvaluator's own Trim+OrdinalIgnoreCase
        // comparison -- two forms that would match the same ComparisonValue must not produce two
        // warnings.
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("G6306B-S1", "SOUPAPE ", NullLogger.Instance, errors);
        tracker.RecordIfNew("G6306B-S2", "soupape", NullLogger.Instance, errors);

        errors.Should().ContainSingle().Which.ExtractedValue.Should().Be("SOUPAPE ");
    }

    [Fact]
    public void RecordIfNew_TwoBlankValues_ProducesExactlyOneEntry()
    {
        // 55.5 case d.
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("C7401-V1", "", NullLogger.Instance, errors);
        tracker.RecordIfNew("C7401-V2", null, NullLogger.Instance, errors);

        errors.Should().ContainSingle();
    }

    [Fact]
    public void RecordIfNew_SameValueOnTwoSeparateTrackerInstances_ProducesOneEntryEach()
    {
        // 55.5 case e: the dedup key is (feuille, valeur normalisée), never the value alone -- a
        // tracker is instantiated fresh per sheet/per Extract() call, never shared.
        var isolementTracker = new NoConditionalPointCreatedWarningTracker("ISOLEMENT");
        var diversTracker = new NoConditionalPointCreatedWarningTracker("DIVERS");
        var errors = new List<ExtractionError>();

        isolementTracker.RecordIfNew("C7401-V1", "PROLOCK", NullLogger.Instance, errors);
        diversTracker.RecordIfNew("C7401-D1", "PROLOCK", NullLogger.Instance, errors);

        errors.Should().HaveCount(2);
        errors.Select(e => e.Sheet).Should().BeEquivalentTo(["ISOLEMENT", "DIVERS"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordIfNew_BlankOrNullValue_MessageReflectsAbsenceWithoutEmptyQuotes(string? blankValue)
    {
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("C7401-V1", blankValue, NullLogger.Instance, errors);

        var message = errors.Should().ContainSingle().Subject.Message;
        message.Should().NotContain("« »");
        message.Should().NotContain("Colonne");
    }

    [Fact]
    public void RecordIfNew_NeverAssertsAnythingAboutTheOxoReferential()
    {
        // §Décisions actées : le moteur ne juge que le profil, jamais le référentiel OXO -- ni
        // "inconnu", ni "non reconnu", ni "erreur de saisie".
        var tracker = new NoConditionalPointCreatedWarningTracker(Sheet);
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("C7401-V1", "PROLOCK", NullLogger.Instance, errors);

        var message = errors.Should().ContainSingle().Subject.Message;
        message.Should().NotContainEquivalentOf("inconnu");
        message.Should().NotContainEquivalentOf("non reconnu");
        message.Should().NotContainEquivalentOf("erreur de saisie");
    }
}
