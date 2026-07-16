using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

// The generic engine behind RepeatingBlockLocator, shared by all 6 source sheets (see
// docs/modele-domaine-import-profile-2026-07-16.md §1.2). Reads the stop field first and bails out
// before touching the block's other fields, both for efficiency and so a block that fails the stop
// check never gets misreported as a "partially empty" one.
public sealed class RepeatingBlockReader : IRepeatingBlockReader
{
    public RepeatingBlockReadResult Read(RepeatingBlockLocator locator, IWorkbookReader workbookReader)
    {
        var stopField = locator.Fields.First(f => f.Name == locator.StopFieldName);
        var otherFields = locator.Fields.Where(f => f.Name != locator.StopFieldName).ToList();

        var blocks = new List<IReadOnlyDictionary<string, string>>();
        var errors = new List<ExtractionError>();
        var blockIndex = 0;

        while (true)
        {
            var blockStartRow = locator.FirstBlockStartRow + blockIndex * locator.Step;
            var stopValue = workbookReader.ReadCellValue(locator.Sheet, BuildRange(stopField, blockStartRow));

            if (string.IsNullOrWhiteSpace(stopValue))
            {
                break;
            }

            var rawValues = new Dictionary<string, string> { [stopField.Name] = stopValue };
            var blankFieldNames = new List<string>();

            foreach (var field in otherFields)
            {
                var value = workbookReader.ReadCellValue(locator.Sheet, BuildRange(field, blockStartRow));
                if (string.IsNullOrWhiteSpace(value))
                {
                    blankFieldNames.Add(field.Name);
                }
                else
                {
                    rawValues[field.Name] = value;
                }
            }

            if (blankFieldNames.Count > 0)
            {
                errors.Add(new ExtractionError(
                    locator.Sheet, blockStartRow.ToString(), ExtractionErrorCode.RequiredFieldMissing,
                    $"Block at row {blockStartRow} has required field(s) '{string.Join(", ", blankFieldNames)}' " +
                    $"empty while stop field '{locator.StopFieldName}' is populated."));
            }
            else
            {
                blocks.Add(rawValues);
            }

            blockIndex++;
        }

        return new RepeatingBlockReadResult(blocks, errors);
    }

    private static string BuildRange(BlockFieldDefinition field, int blockStartRow)
    {
        var (startColumn, endColumn) = SplitColumnRange(field.ColumnRange);
        var start = $"{startColumn}{blockStartRow + field.RowOffsetStart}";
        var end = $"{endColumn}{blockStartRow + field.RowOffsetEnd}";
        return start == end ? start : $"{start}:{end}";
    }

    private static (string Start, string End) SplitColumnRange(string columnRange)
    {
        var parts = columnRange.Split(':');
        return parts.Length == 1 ? (parts[0], parts[0]) : (parts[0], parts[1]);
    }
}
