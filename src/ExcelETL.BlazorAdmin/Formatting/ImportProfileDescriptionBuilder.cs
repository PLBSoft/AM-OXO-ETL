using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Localization;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): turns an
// ImportProfile into plain-language sentences for a user who is neither a developer nor an expert of
// the application. Presentation only: no rule is rebuilt or validated here. Profile values (column
// names, compared values) are data and are never translated; only the sentence templates come from
// the .resx (ImportProfileDetails_* keys, French text in both files for now -- decision D5).
public static class ImportProfileDescriptionBuilder
{
    private const string ListSeparator = ", ";

    public static ImportProfileDescription Build(ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(loc);

        return new ImportProfileDescription([BuildGeneralSection(profile, loc)]);
    }

    private static ProfileDescriptionSection BuildGeneralSection(ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>
        {
            new(loc["ImportProfileDetails_GeneralReperePrefix", Quote(profile.ReperePrefix, loc)]),
            new(loc["ImportProfileDetails_GeneralEquipementTypeElement", Quote(profile.EquipementTypeElementNom, loc)]),
            new(CountedSentence(
                profile.DefaultTableaux, loc, "ImportProfileDetails_GeneralNoTableau",
                "ImportProfileDetails_GeneralOneTableau", "ImportProfileDetails_GeneralSeveralTableaux")),
            new(CountedSentence(
                profile.DefaultApplicationNames, loc, "ImportProfileDetails_GeneralNoApplication",
                "ImportProfileDetails_GeneralOneApplication", "ImportProfileDetails_GeneralSeveralApplications")),
        };

        sentences.AddRange(profile.TacheMultipleTypeLabels.Select(label => new ProfileDescriptionSentence(
            loc["ImportProfileDetails_GeneralTacheMultipleTypeLabel", Quote(label.Code, loc), Quote(label.Label, loc)])));

        return new ProfileDescriptionSection(loc["ImportProfileDetails_GeneralSectionTitle"], sentences, [], []);
    }

    // Zero / one / several: the "several" template takes the count as {0} and the quoted list as {1}.
    private static string CountedSentence(
        IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc, string noneKey, string oneKey, string severalKey) =>
        values.Count switch
        {
            0 => loc[noneKey],
            1 => loc[oneKey, Quote(values[0], loc)],
            _ => loc[severalKey, values.Count, QuoteList(values, loc)],
        };

    private static string Quote(string value, IStringLocalizer<BlazorAdminMessages> loc) =>
        loc["ImportProfileDetails_QuotedValue", value];

    private static string QuoteList(IEnumerable<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        string.Join(ListSeparator, values.Select(value => Quote(value, loc)));
}
