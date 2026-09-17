namespace ExcelETL.Domain.Generation.Profile;

// Lot 080.1 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md): the worksheet names
// ClosedXML refuses when a workbook is written (ArgumentException). Single source of these rules: the export
// profile validates its sheet names with it, the generation engine checks the names it produces, and
// ExcelSheetNameWriterAgreementTests (Infrastructure.Tests) checks it against the real writer.
// Leading/trailing spaces are accepted and kept by ClosedXML: no check on them.
public static class ExcelSheetName
{
    public const int MaxLength = 31;

    public static IReadOnlyList<char> ForbiddenCharacters { get; } = ['\\', '/', '?', '*', '[', ']', ':'];

    public static bool IsTooLong(string name) => name.Length > MaxLength;

    // Distinct forbidden characters, in order of first appearance.
    public static IReadOnlyList<char> ForbiddenCharactersIn(string name) =>
        [.. name.Where(ForbiddenCharacters.Contains).Distinct()];

    public static bool HasApostropheAtEdge(string name) => name.StartsWith('\'') || name.EndsWith('\'');

    public static bool IsValid(string name) =>
        !string.IsNullOrEmpty(name)
        && !IsTooLong(name)
        && ForbiddenCharactersIn(name).Count == 0
        && !HasApostropheAtEdge(name);

    // Two worksheets can't share a name, case ignored.
    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;
}
