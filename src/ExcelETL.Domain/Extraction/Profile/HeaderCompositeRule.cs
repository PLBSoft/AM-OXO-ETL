using System.Text.RegularExpressions;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Profile;

// A header field derived from a text template with named placeholders (Lot 047,
// spec-migration-entetes-profile-driven-directcell.md §3), e.g. "Rév {revision} du {dateRev}" where
// each {placeholder} references the Name of a HeaderFieldRule on the same SheetExtractionRule.
// Deliberately not the recursive TextTransform tree -- a flat template string, trivially persistable
// and editable (Lot 048).
public sealed partial record HeaderCompositeRule
{
    public string Name { get; }
    public string Template { get; }

    public HeaderCompositeRule(string name, string template)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.HeaderCompositeRule_EmptyName);
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new DomainValidationException(
                "Template must not be empty.", nameof(template), DomainErrorCode.HeaderCompositeRule_EmptyTemplate);
        }

        Name = name;
        Template = template;
    }

    // Every {placeholder} name referenced by Template, in order of first appearance. Used both by
    // SheetExtractionRule's construction-time cross-validation (recommendation, ticket 47.1) and by
    // the Application-layer header resolver's own defensive substitution (ticket 47.2).
    public IReadOnlyList<string> PlaceholderNames() =>
        [.. PlaceholderPattern().Matches(Template).Select(m => m.Groups[1].Value).Distinct()];

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PlaceholderPattern();
}
