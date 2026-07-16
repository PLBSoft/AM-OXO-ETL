namespace ExcelETL.Domain.Extraction.Primitives;

// A fixed literal segment in a Concat transform (e.g. the "-" separator in a composed repère).
public sealed record Literal(string Text) : ConcatPart;
