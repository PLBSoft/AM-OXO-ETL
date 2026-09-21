using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// Purpose-built output of one IRepeatingBlockReader.Read call -- same spirit as the pre-existing
// ExtractionResult DTO (Extraction/ExtractionResult.cs), not a generic Result<T> wrapper. Raw field
// values only -- building the pivot from them is the caller's job.
public sealed record RepeatingBlockReadResult(
    IReadOnlyList<RepeatingBlock> Blocks,
    IReadOnlyList<ExtractionError> Errors);

// StartRow identifies the block (e.g. in a RequiredFieldMissing error or a caller's own report). It
// can't be recomputed from a block's
// position within Blocks alone: a block with a required-field error is dropped from Blocks but still
// consumes a row, so later blocks' list index no longer lines up with their real row.
public sealed record RepeatingBlock(int StartRow, IReadOnlyDictionary<string, string> Fields);
