using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 079.2 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): the columns of
// one generated sheet, in the order SheetGenerationEngine writes them -- descriptive, constant, application,
// then point columns, each group in its list's order. Duplicated knowledge on purpose; ExportColumnLayoutTests
// compares it with the real engine's headers on the seeded profile.
public static class ExportColumnLayout
{
    public static IReadOnlyList<ExportColumn> For(SheetGenerationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        IEnumerable<(string Header, ExportColumnKind Kind, object Definition)> columns = rule.ColumnDefinitions
            .Select(c => (c.Header, ExportColumnKind.Descriptive, (object)c))
            .Concat(rule.ConstantColumnDefinitions.Select(c => (c.Header, ExportColumnKind.Constant, (object)c)))
            .Concat(rule.ApplicationColumnDefinitions.Select(c => (c.Header, ExportColumnKind.Application, (object)c)))
            .Concat(rule.PointColumnDefinitions.Select(c => (c.Header, ExportColumnKind.Point, (object)c)));

        return [.. columns.Select((c, index) => new ExportColumn(ExcelColumnLetters.FromNumber(index + 1), c.Header, c.Kind, c.Definition))];
    }
}

public enum ExportColumnKind
{
    Descriptive,
    Constant,
    Application,
    Point
}

// Definition is the column's own profile record: ColumnDefinition, ConstantColumnDefinition,
// ApplicationColumnDefinition or PointColumnDefinition, matching Kind.
public sealed record ExportColumn(string Letter, string Header, ExportColumnKind Kind, object Definition);
