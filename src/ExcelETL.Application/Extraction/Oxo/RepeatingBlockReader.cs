using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

// The generic engine behind RepeatingBlockLocator, shared by all 6 source sheets (see
// docs/modele-domaine-import-profile-2026-07-16.md §1.2). Reads the stop field first and bails out
// before touching the block's other fields, both for efficiency and so a block that fails the stop
// check never gets misreported as a "partially empty" one. The stop field is the caller's (lot 084, G12):
// the locator carries no stop-field setting.
public sealed class RepeatingBlockReader : IRepeatingBlockReader
{
    public RepeatingBlockReadResult Read(RepeatingBlockLocator locator, string stopFieldName, IWorkbookReader workbookReader)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentException.ThrowIfNullOrWhiteSpace(stopFieldName);
        ArgumentNullException.ThrowIfNull(workbookReader);

        var stopField = locator.Fields.FirstOrDefault(f => f.Name == stopFieldName)
            ?? throw new UnknownFieldReferenceException(stopFieldName);
        var otherFields = locator.Fields.Where(f => f.Name != stopFieldName).ToList();

        var blocks = new List<RepeatingBlock>();
        var errors = new List<ExtractionError>();
        var blockIndex = 0;

        while (true)
        {
            var blockStartRow = locator.FirstBlockStartRow + blockIndex * locator.Step;
            var stopValue = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(stopField, blockStartRow));

            if (string.IsNullOrWhiteSpace(stopValue))
            {
                break;
            }

            var rawValues = new Dictionary<string, string> { [stopField.Name] = stopValue };
            var blankFieldNames = new List<string>();

            foreach (var field in otherFields)
            {
                var value = workbookReader.ReadCellValue(
                    locator.Sheet, BlockFieldRangeCalculator.BuildRange(field, blockStartRow));
                if (string.IsNullOrWhiteSpace(value))
                {
                    // Lot 084 (G4): an optional field left blank is read as "" and keeps the block.
                    if (field.IsRequired)
                    {
                        blankFieldNames.Add(field.Name);
                    }
                    else
                    {
                        rawValues[field.Name] = "";
                    }
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
                    $"empty while stop field '{stopFieldName}' is populated."));
            }
            else
            {
                blocks.Add(new RepeatingBlock(blockStartRow, rawValues));
            }

            blockIndex++;
        }

        return new RepeatingBlockReadResult(blocks, errors);
    }
}
