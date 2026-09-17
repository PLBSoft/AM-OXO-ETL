using ExcelETL.BlazorAdmin.Resources;
using Microsoft.Extensions.Localization;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 079.1 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): helpers shared
// by the import and export description builders. They build "marked" strings: profile values and cell
// coordinates wrapped in private-use characters so they survive the template formatting as their own
// segments (see ProfileDescriptionText.FromMarked).
internal static class ProfileDescriptionMarking
{
    internal const string ListSeparator = ", ";

    // A cell coordinate, marked the same way as a value (Lot 078.12.3).
    internal static string CellRef(string range) =>
        ProfileDescriptionText.CellStart + range + ProfileDescriptionText.CellEnd;

    // The value is wrapped in markers so it survives the template formatting as its own segment.
    internal static string Quote(string value, IStringLocalizer<BlazorAdminMessages> loc) =>
        loc["ProfileDescription_QuotedValue", ProfileDescriptionText.ValueStart + value + ProfileDescriptionText.ValueEnd];

    internal static List<ProfileDescriptionText> Marked(IEnumerable<string> markedTexts) =>
        [.. markedTexts.Select(ProfileDescriptionText.FromMarked)];

    // "a", "a et b", "a, b et c".
    internal static string JoinWithAnd(IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        values.Count == 1
            ? values[0]
            : string.Join(ListSeparator, values.Take(values.Count - 1)) + loc["ProfileDescription_ListLastSeparator"] + values[^1];

    // "a", "a ou b", "a, b ou c".
    internal static string JoinWithOr(IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        values.Count == 1
            ? values[0]
            : string.Join(ListSeparator, values.Take(values.Count - 1)) + loc["ProfileDescription_ListLastOrSeparator"] + values[^1];

    internal static string QuoteList(IEnumerable<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        string.Join(ListSeparator, values.Select(value => Quote(value, loc)));

    // One / several: the "several" template takes the count as {0} and the quoted list as {1}.
    internal static string OneOrSeveral(IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc, string oneKey, string severalKey) =>
        values.Count == 1 ? loc[oneKey, Quote(values[0], loc)] : loc[severalKey, values.Count, QuoteList(values, loc)];
}
