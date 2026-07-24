namespace ExcelETL.Application.Extraction.Oxo.Procedure;

// A contiguous run of PROCEDURE tasks sharing the same (normalized) TypeTacheMultipleCode within one
// section, in task order. Type keeps the first task's original (non-normalized) value, for display.
public sealed record TypeRun(string Type, int OrdreDebut, int OrdreFin);

// Sandwich (a run boundary on both sides) and the two "bord de section" cases carry the same weight
// (ticket decision 7) -- DebutDeSection/FinDeSection only exist to pick the right wording, neither is
// "less reliable" than Sandwich.
public enum TypeRunPosition
{
    Sandwich,
    DebutDeSection,
    FinDeSection
}

public sealed record MinorityTypeRunAnomaly(TypeRun Run, TypeRunPosition Position);

// One of the (>= 2) types tied for the largest task count in a strictly-ambiguous section (decision
// 6) -- Runs is plural because a tied type can itself span more than one run. Overrides
// Equals/GetHashCode (SequenceEqual on Runs) because default record equality on IReadOnlyList<T> is
// reference equality, not structural -- same reason as RepeatingBlockLocator/Concat in Domain.
public sealed record AmbiguousTypeGroup(string Type, IReadOnlyList<TypeRun> Runs)
{
    public bool Equals(AmbiguousTypeGroup? other) =>
        other is not null && Type == other.Type && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        foreach (var run in Runs)
        {
            hash.Add(run);
        }

        return hash.ToHashCode();
    }
}

// Exactly one of MinorityRunAnomalies/AmbiguousGroups is ever populated -- a clear majority type
// decomposes into minority-run anomalies (decision 5), a strict tie (decision 6) decomposes into
// ambiguous groups instead, and both empty means the section is a single, perfectly homogeneous run
// (no anomaly at all).
public sealed record TacheMultipleTypeCoherenceAnalysis(
    string? MajorityType,
    IReadOnlyList<MinorityTypeRunAnomaly> MinorityRunAnomalies,
    IReadOnlyList<AmbiguousTypeGroup> AmbiguousGroups);

// Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md): detects a TYPE
// inconsistency within one PROCEDURE TacheMultiple section -- a client data-entry-quality guard-rail
// (ticket decision 3), not a business rule, hence not part of the ImportProfile catalogue. Pure and
// ClosedXML-free (note d'efficacité #1) so the bulk of its coverage (edge cases, strict ties, multiple
// minority runs) is validated here rather than against real fixtures.
public static class TacheMultipleTypeCoherenceAnalyzer
{
    public static IReadOnlyList<TypeRun> ComputeRuns(IReadOnlyList<(int Ordre, string Type)> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var runs = new List<TypeRun>();
        string? currentNormalizedType = null;
        var currentType = "";
        var currentStart = 0;
        var currentEnd = 0;

        foreach (var (ordre, type) in tasks)
        {
            var normalized = Normalize(type);
            if (currentNormalizedType is null)
            {
                (currentNormalizedType, currentType, currentStart, currentEnd) = (normalized, type, ordre, ordre);
            }
            else if (normalized == currentNormalizedType)
            {
                currentEnd = ordre;
            }
            else
            {
                runs.Add(new TypeRun(currentType, currentStart, currentEnd));
                (currentNormalizedType, currentType, currentStart, currentEnd) = (normalized, type, ordre, ordre);
            }
        }

        if (currentNormalizedType is not null)
        {
            runs.Add(new TypeRun(currentType, currentStart, currentEnd));
        }

        return runs;
    }

    public static TacheMultipleTypeCoherenceAnalysis Analyze(IReadOnlyList<(int Ordre, string Type)> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var runs = ComputeRuns(tasks);
        if (runs.Count <= 1)
        {
            return new TacheMultipleTypeCoherenceAnalysis(null, [], []);
        }

        var totalsByType = tasks
            .GroupBy(t => Normalize(t.Type))
            .Select(g => (Type: g.Key, Count: g.Count()))
            .ToList();
        var maxCount = totalsByType.Max(t => t.Count);
        var topTypes = totalsByType.Where(t => t.Count == maxCount).Select(t => t.Type).ToList();

        if (topTypes.Count > 1)
        {
            var groups = topTypes
                .Select(type => new AmbiguousTypeGroup(
                    OriginalType(tasks, type), runs.Where(r => Normalize(r.Type) == type).ToList()))
                .ToList();
            return new TacheMultipleTypeCoherenceAnalysis(null, [], groups);
        }

        var majorityType = topTypes[0];
        var anomalies = new List<MinorityTypeRunAnomaly>();
        for (var i = 0; i < runs.Count; i++)
        {
            if (Normalize(runs[i].Type) == majorityType)
            {
                continue;
            }

            var hasRunBefore = i > 0;
            var hasRunAfter = i < runs.Count - 1;
            var position = (hasRunBefore, hasRunAfter) switch
            {
                (true, true) => TypeRunPosition.Sandwich,
                (false, _) => TypeRunPosition.DebutDeSection,
                (true, false) => TypeRunPosition.FinDeSection
            };

            anomalies.Add(new MinorityTypeRunAnomaly(runs[i], position));
        }

        return new TacheMultipleTypeCoherenceAnalysis(OriginalType(tasks, majorityType), anomalies, []);
    }

    private static string OriginalType(IReadOnlyList<(int Ordre, string Type)> tasks, string normalizedType) =>
        tasks.First(t => Normalize(t.Type) == normalizedType).Type;

    private static string Normalize(string type) => type.Trim().ToUpperInvariant();
}
