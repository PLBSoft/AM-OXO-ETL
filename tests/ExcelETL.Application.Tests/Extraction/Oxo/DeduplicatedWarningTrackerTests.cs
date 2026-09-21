using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

// Lot 084.4 (G11): the one tracker replacing the per-warning classes.
public class DeduplicatedWarningTrackerTests
{
    [Fact]
    public void RecordIfNew_SameValueTrimmedAndCaseInsensitive_IsReportedOnce_WithTheFirstRawForm()
    {
        var tracker = new DeduplicatedWarningTracker("PLATINES", ExtractionErrorCode.UnexpectedCouleurEtiquetteValue, v => $"m:{v}");
        var errors = new List<ExtractionError>();

        tracker.RecordIfNew("A-1", " Date", NullLogger.Instance, errors);
        tracker.RecordIfNew("A-2", "DATE ", NullLogger.Instance, errors);
        tracker.RecordIfNew("A-3", "ROSE", NullLogger.Instance, errors);

        errors.Should().HaveCount(2);
        errors[0].Should().Be(new ExtractionError(
            "PLATINES", "A-1", ExtractionErrorCode.UnexpectedCouleurEtiquetteValue, "m: Date", " Date"));
        errors[1].ExtractedValue.Should().Be("ROSE");
    }

    [Fact]
    public void ForNoConditionalPointCreated_KeepsTheLot055Messages()
    {
        var errors = new List<ExtractionError>();
        var tracker = DeduplicatedWarningTracker.ForNoConditionalPointCreated("ISOLEMENT");

        tracker.RecordIfNew("C7401-V1", "PROLOCK", NullLogger.Instance, errors);
        tracker.RecordIfNew("C7401-V2", null, NullLogger.Instance, errors);

        errors.Select(e => e.Message).Should().Equal(
            "Aucun Point conditionnel n'a été créé pour la valeur « PROLOCK » : aucune condition du profil d'import " +
            "ne correspond à cette valeur pour cette feuille.",
            "Aucun Point conditionnel n'a été créé : aucune valeur n'a été extraite pour cette feuille, " +
            "et aucune condition du profil d'import ne correspond à une valeur absente.");
        errors.Should().OnlyContain(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated);
    }
}
