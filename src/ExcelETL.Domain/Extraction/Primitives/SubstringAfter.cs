using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// Strips a fixed prefix from the extracted value (e.g. the paramétrable "MAD-OXO-" repère prefix).
public sealed record SubstringAfter : TextTransform
{
    public string Prefix { get; }

    public SubstringAfter(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new DomainValidationException(
                "Prefix must not be empty.", nameof(prefix), DomainErrorCode.SubstringAfter_EmptyPrefix);
        }

        Prefix = prefix;
    }
}
