using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// Purpose-built output of one IRepeatingBlockReader.Read call -- same spirit as the pre-existing
// ExtractionResult DTO (Extraction/ExtractionResult.cs), not a generic Result<T> wrapper. Raw field
// values only (TextTransform has not run yet -- that's the caller's job, using
// ITextTransformEvaluator).
public sealed record RepeatingBlockReadResult(
    IReadOnlyList<RepeatingBlock> Blocks,
    IReadOnlyList<ExtractionError> Errors);

// StartRow is needed by a caller that wants to read a cell of its own (beyond the locator's declared
// Fields) for a specific block -- e.g. UnconditionalIsolementSheetExtractionService's
// FieldPresencePointRules (PLATINES client feedback, 2026-09). It can't be recomputed from a block's
// position within Blocks alone: a block with a required-field error is dropped from Blocks but still
// consumes a row, so later blocks' list index no longer lines up with their real row.
public sealed record RepeatingBlock(int StartRow, IReadOnlyDictionary<string, string> Fields);
