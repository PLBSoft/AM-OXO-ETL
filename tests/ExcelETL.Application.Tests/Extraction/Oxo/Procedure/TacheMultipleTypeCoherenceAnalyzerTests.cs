using ExcelETL.Application.Extraction.Oxo.Procedure;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Procedure;

// Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md), 32.1 (run computation +
// majority determination) and 32.2 (sandwich vs. bord classification) -- pure, no ClosedXML/fixture
// dependency, per the ticket's own "note d'efficacité" #1.
public class TacheMultipleTypeCoherenceAnalyzerTests
{
    private static IReadOnlyList<(int Ordre, string Type)> Tasks(string type, int fromOrdre, int toOrdre) =>
        Enumerable.Range(fromOrdre, toOrdre - fromOrdre + 1).Select(o => (o, type)).ToList();

    private static IReadOnlyList<(int Ordre, string Type)> Concat(params IReadOnlyList<(int Ordre, string Type)>[] runs) =>
        runs.SelectMany(r => r).ToList();

    // 32.1 -- run computation and majority determination

    [Fact]
    public void Analyze_WithASingleRun_ReturnsNoAnomaly()
    {
        var tasks = Tasks("TM_PROC_REL", 1, 20);

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MinorityRunAnomalies.Should().BeEmpty();
        result.AmbiguousGroups.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_ReproducingTheC7401Pattern_IdentifiesRelAsMajorityAndMadAsTheOnlyMinorityRun()
    {
        // 24 REL / 6 MAD / 10 REL, the exact shape of the real C7401 anomaly (tasks 49-72/73-78/79-88).
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 24), Tasks("TM_PROC_MAD", 25, 30), Tasks("TM_PROC_REL", 31, 40));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MajorityType.Should().Be("TM_PROC_REL");
        result.MinorityRunAnomalies.Should().ContainSingle();
        result.MinorityRunAnomalies[0].Run.Should().Be(new TypeRun("TM_PROC_MAD", 25, 30));
    }

    [Fact]
    public void Analyze_WithMinorityRunAtTheStart_IdentifiesItAsTheOnlyMinorityRunWithNoRunBeforeIt()
    {
        var tasks = Concat(Tasks("TM_PROC_MAD", 1, 3), Tasks("TM_PROC_REL", 4, 23));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MajorityType.Should().Be("TM_PROC_REL");
        result.MinorityRunAnomalies.Should().ContainSingle();
        result.MinorityRunAnomalies[0].Run.Should().Be(new TypeRun("TM_PROC_MAD", 1, 3));
    }

    [Fact]
    public void Analyze_WithMinorityRunAtTheEnd_IdentifiesItAsTheOnlyMinorityRunWithNoRunAfterIt()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 20), Tasks("TM_PROC_MAD", 21, 23));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MajorityType.Should().Be("TM_PROC_REL");
        result.MinorityRunAnomalies.Should().ContainSingle();
        result.MinorityRunAnomalies[0].Run.Should().Be(new TypeRun("TM_PROC_MAD", 21, 23));
    }

    [Fact]
    public void Analyze_WithTwoDistinctMinorityRuns_ReturnsTwoDistinctAnomaliesEachWithItsOwnRange()
    {
        var tasks = Concat(
            Tasks("TM_PROC_MAD", 1, 3), Tasks("TM_PROC_REL", 4, 23), Tasks("TM_PROC_MAD", 24, 25));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MajorityType.Should().Be("TM_PROC_REL");
        result.MinorityRunAnomalies.Should().HaveCount(2);
        result.MinorityRunAnomalies[0].Run.Should().Be(new TypeRun("TM_PROC_MAD", 1, 3));
        result.MinorityRunAnomalies[1].Run.Should().Be(new TypeRun("TM_PROC_MAD", 24, 25));
    }

    [Fact]
    public void Analyze_WithStrictlyEqualSplit_ReturnsAnAmbiguousGroupForEachTiedTypeInsteadOfMinorityRuns()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 10), Tasks("TM_PROC_MAD", 11, 20));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MinorityRunAnomalies.Should().BeEmpty();
        result.MajorityType.Should().BeNull();
        result.AmbiguousGroups.Should().HaveCount(2);
        result.AmbiguousGroups[0].Should().Be(new AmbiguousTypeGroup("TM_PROC_REL", [new TypeRun("TM_PROC_REL", 1, 10)]));
        result.AmbiguousGroups[1].Should().Be(new AmbiguousTypeGroup("TM_PROC_MAD", [new TypeRun("TM_PROC_MAD", 11, 20)]));
    }

    [Fact]
    public void Analyze_WithThreeWayStrictTie_ReturnsOneAmbiguousGroupPerTiedType()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 5), Tasks("TM_PROC_MAD", 6, 10), Tasks("XYZ", 11, 15));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.AmbiguousGroups.Should().HaveCount(3);
        result.AmbiguousGroups.Select(g => g.Type).Should().Equal("TM_PROC_REL", "TM_PROC_MAD", "XYZ");
    }

    [Fact]
    public void Analyze_ComparesTypesTrimmedAndCaseInsensitive()
    {
        var tasks = Concat(Tasks("rel", 1, 20), Tasks(" REL ", 21, 23));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        // A single logical type once normalized -> a single run, no anomaly.
        result.MinorityRunAnomalies.Should().BeEmpty();
        result.AmbiguousGroups.Should().BeEmpty();
    }

    // 32.2 -- sandwich vs. bord classification

    [Fact]
    public void Analyze_WithMinorityRunSurroundedOnBothSides_ClassifiesItAsSandwich()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 24), Tasks("TM_PROC_MAD", 25, 30), Tasks("TM_PROC_REL", 31, 40));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MinorityRunAnomalies[0].Position.Should().Be(TypeRunPosition.Sandwich);
    }

    [Fact]
    public void Analyze_WithMinorityRunAtTheStart_ClassifiesItAsDebutDeSection()
    {
        var tasks = Concat(Tasks("TM_PROC_MAD", 1, 3), Tasks("TM_PROC_REL", 4, 23));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MinorityRunAnomalies[0].Position.Should().Be(TypeRunPosition.DebutDeSection);
    }

    [Fact]
    public void Analyze_WithMinorityRunAtTheEnd_ClassifiesItAsFinDeSection()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 20), Tasks("TM_PROC_MAD", 21, 23));

        var result = TacheMultipleTypeCoherenceAnalyzer.Analyze(tasks);

        result.MinorityRunAnomalies[0].Position.Should().Be(TypeRunPosition.FinDeSection);
    }

    // ComputeRuns -- the pure run-computation building block (ticket 32.1, first bullet)

    [Fact]
    public void ComputeRuns_WithHomogeneousTasks_ReturnsASingleRun()
    {
        var tasks = Tasks("TM_PROC_REL", 1, 5);

        var runs = TacheMultipleTypeCoherenceAnalyzer.ComputeRuns(tasks);

        runs.Should().Equal(new TypeRun("TM_PROC_REL", 1, 5));
    }

    [Fact]
    public void ComputeRuns_WithAlternatingTypes_ReturnsOneRunPerContiguousGroup()
    {
        var tasks = Concat(Tasks("TM_PROC_REL", 1, 2), Tasks("TM_PROC_MAD", 3, 3), Tasks("TM_PROC_REL", 4, 5));

        var runs = TacheMultipleTypeCoherenceAnalyzer.ComputeRuns(tasks);

        runs.Should().Equal(
            new TypeRun("TM_PROC_REL", 1, 2), new TypeRun("TM_PROC_MAD", 3, 3), new TypeRun("TM_PROC_REL", 4, 5));
    }
}
