using System.Text;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public sealed class TextTransformEvaluator : ITextTransformEvaluator
{
    public (string? Value, string? ErrorMessage) Evaluate(
        TextTransform transform, string? rawValue, IReadOnlyDictionary<string, string> extractedFields) =>
        transform switch
        {
            RawValue => (rawValue, null),
            SubstringAfter substringAfter => EvaluateSubstringAfter(substringAfter, rawValue),
            Concat concat => (EvaluateConcat(concat, extractedFields), null),
            _ => throw new NotSupportedException($"Unsupported text transform type '{transform.GetType().Name}'.")
        };

    private static (string? Value, string? ErrorMessage) EvaluateSubstringAfter(SubstringAfter transform, string? rawValue)
    {
        if (rawValue is null || !rawValue.StartsWith(transform.Prefix, StringComparison.Ordinal))
        {
            return (null, $"Value '{rawValue}' does not start with expected prefix '{transform.Prefix}'.");
        }

        return (rawValue[transform.Prefix.Length..], null);
    }

    private static string EvaluateConcat(Concat transform, IReadOnlyDictionary<string, string> extractedFields)
    {
        var builder = new StringBuilder();

        foreach (var part in transform.Parts)
        {
            switch (part)
            {
                case Literal literal:
                    builder.Append(literal.Text);
                    break;
                case FieldRef fieldRef:
                    if (!extractedFields.TryGetValue(fieldRef.FieldName, out var value))
                    {
                        throw new UnknownFieldReferenceException(fieldRef.FieldName);
                    }

                    builder.Append(value);
                    break;
            }
        }

        return builder.ToString();
    }
}
