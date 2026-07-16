using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// Combines Literal and FieldRef parts into one value (e.g. Designation, composed repère isolement).
// Parts is a list, so the default record-synthesized equality (reference equality on the list) is
// overridden below with SequenceEqual to give true structural equality.
public sealed record Concat : TextTransform
{
    public IReadOnlyList<ConcatPart> Parts { get; }

    public Concat(IReadOnlyList<ConcatPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 0)
        {
            throw new DomainValidationException(
                "Parts must contain at least one part.", nameof(parts), DomainErrorCode.Concat_EmptyParts);
        }

        Parts = parts;
    }

    public bool Equals(Concat? other) => other is not null && Parts.SequenceEqual(other.Parts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var part in Parts)
        {
            hash.Add(part);
        }

        return hash.ToHashCode();
    }
}
