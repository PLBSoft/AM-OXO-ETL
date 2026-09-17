namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 079.6 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): the sheet names
// ClosedXML refuses when ClosedXmlWorkbookWriter adds a worksheet (ArgumentException). Duplicated knowledge on
// purpose -- the domain accepts these names; ExcelSheetNameRulesTests writes each case with the real writer.
public static class ExcelSheetNameRules
{
    public const int MaxLength = 31;

    private static readonly char[] ForbiddenCharacters = ['\\', '/', '?', '*', '[', ']', ':'];

    public static bool IsTooLong(string name) => name.Length > MaxLength;

    // Distinct forbidden characters, in order of first appearance.
    public static IReadOnlyList<char> ForbiddenCharactersIn(string name) =>
        [.. name.Where(ForbiddenCharacters.Contains).Distinct()];

    public static bool HasApostropheAtEdge(string name) => name.StartsWith('\'') || name.EndsWith('\'');

    // Two worksheets can't share a name, case ignored.
    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;
}
