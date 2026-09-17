namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): the plain
// language description of an ImportProfile, built by ImportProfileDescriptionBuilder. Text only, never
// markup -- rendering belongs to ImportProfileDetails.razor.
public sealed record ImportProfileDescription(IReadOnlyList<ProfileDescriptionSection> Sections);

// Ignored: settings stored in the profile that extraction doesn't use for this sheet (D2).
// Blocking: problems that make extraction fail.
public sealed record ProfileDescriptionSection(
    string Title,
    IReadOnlyList<ProfileDescriptionSentence> Sentences,
    IReadOnlyList<string> Ignored,
    IReadOnlyList<string> Blocking);

// IsFixed: behavior coded in the extraction services, not editable in the profile (D3) -- the page adds
// the "non modifiable" mark, the text itself doesn't carry it.
public sealed record ProfileDescriptionSentence(string Text, bool IsFixed = false);
