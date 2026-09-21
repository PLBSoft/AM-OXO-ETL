namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): the plain
// language description of a profile, built by ImportProfileDescriptionBuilder (and, since lot 079, by
// ExportProfileDescriptionBuilder). Text only, never markup -- rendering belongs to ProfileDescriptionView.razor.
public sealed record ProfileDescription(IReadOnlyList<ProfileDescriptionSection> Sections);

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
// Lot 078.12.2/3: a text split into plain parts, profile values (a Tableau name, a compared value...) and
// cell coordinates, so the page can emphasise each. The guillemets around a value stay plain text.
public sealed record ProfileDescriptionText(IReadOnlyList<ProfileDescriptionSegment> Segments)
{
    // Private-use characters wrapped around a value or a cell reference by the builder while it formats a
    // template, then removed here -- the only way to keep track of them through IStringLocalizer's
    // string.Format.
    internal const char ValueStart = (char)0xE000;
    internal const char ValueEnd = (char)0xE001;
    internal const char CellStart = (char)0xE002;
    internal const char CellEnd = (char)0xE003;

    public string Text => string.Concat(Segments.Select(s => s.Text));

    public override string ToString() => Text;

    internal static ProfileDescriptionText FromMarked(string marked)
    {
        var segments = new List<ProfileDescriptionSegment>();
        var position = 0;
        while (position < marked.Length)
        {
            var start = marked.IndexOfAny([ValueStart, CellStart], position);
            if (start < 0)
            {
                segments.Add(new(marked[position..], ProfileDescriptionSegmentKind.Text));
                break;
            }

            if (start > position)
            {
                segments.Add(new(marked[position..start], ProfileDescriptionSegmentKind.Text));
            }

            var (endMarker, kind) = marked[start] == ValueStart
                ? (ValueEnd, ProfileDescriptionSegmentKind.Value)
                : (CellEnd, ProfileDescriptionSegmentKind.CellReference);
            var end = marked.IndexOf(endMarker, start + 1);
            segments.Add(new(marked[(start + 1)..end], kind));
            position = end + 1;
        }

        return new ProfileDescriptionText(segments);
    }
}

public sealed record ProfileDescriptionSegment(string Text, ProfileDescriptionSegmentKind Kind);

public enum ProfileDescriptionSegmentKind
{
    Text,
    Value,
    CellReference
}
