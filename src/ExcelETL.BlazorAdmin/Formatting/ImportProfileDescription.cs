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
    IReadOnlyList<ProfileDescriptionText> Ignored,
    IReadOnlyList<ProfileDescriptionText> Blocking);

// IsFixed: behavior coded in the extraction services, not editable in the profile (D3) -- the page adds
// the "non modifiable" mark, the text itself doesn't carry it.
public sealed record ProfileDescriptionSentence(ProfileDescriptionText Content, bool IsFixed = false)
{
    // The builder formats its sentences as marked strings (see ProfileDescriptionText.FromMarked).
    internal ProfileDescriptionSentence(string MarkedText, bool IsFixed = false)
        : this(ProfileDescriptionText.FromMarked(MarkedText), IsFixed)
    {
    }

    public string Text => Content.Text;
}

// Lot 078.12.2: a text split into plain parts and profile values (a Tableau name, a compared value...),
// so the page can emphasise the values. The guillemets around a value belong to the plain parts.
public sealed record ProfileDescriptionText(IReadOnlyList<ProfileDescriptionSegment> Segments)
{
    // Private-use characters wrapped around a value by the builder while it formats a template, then
    // removed here -- the only way to keep track of a value through IStringLocalizer's string.Format.
    internal const char ValueStart = '';
    internal const char ValueEnd = '';

    public string Text => string.Concat(Segments.Select(s => s.Text));

    public override string ToString() => Text;

    internal static ProfileDescriptionText FromMarked(string marked)
    {
        var segments = new List<ProfileDescriptionSegment>();
        var position = 0;
        while (position < marked.Length)
        {
            var start = marked.IndexOf(ValueStart, position);
            if (start < 0)
            {
                segments.Add(new(marked[position..], false));
                break;
            }

            if (start > position)
            {
                segments.Add(new(marked[position..start], false));
            }

            var end = marked.IndexOf(ValueEnd, start + 1);
            segments.Add(new(marked[(start + 1)..end], true));
            position = end + 1;
        }

        return new ProfileDescriptionText(segments);
    }
}

public sealed record ProfileDescriptionSegment(string Text, bool IsValue);
