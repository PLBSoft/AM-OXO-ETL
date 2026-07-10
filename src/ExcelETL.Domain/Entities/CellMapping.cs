using System.Text.RegularExpressions;
using ExcelETL.Domain.Common;
using ExcelETL.Domain.Enums;

namespace ExcelETL.Domain.Entities;

public partial class CellMapping : Entity
{
    public string SourceCell { get; }
    public string TargetPropertyName { get; }
    public CellDataType DataType { get; }

    public CellMapping(string sourceCell, string targetPropertyName, CellDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(sourceCell) || !ExcelCellReferencePattern().IsMatch(sourceCell))
        {
            throw new ArgumentException(
                "Source cell must be a valid Excel cell reference (e.g. 'B4') or merged range (e.g. 'B4:D4').",
                nameof(sourceCell));
        }

        if (string.IsNullOrWhiteSpace(targetPropertyName))
        {
            throw new ArgumentException("Target property name must not be empty.", nameof(targetPropertyName));
        }

        SourceCell = sourceCell;
        TargetPropertyName = targetPropertyName;
        DataType = dataType;
    }

    [GeneratedRegex(@"^[A-Z]{1,3}[1-9][0-9]*(:[A-Z]{1,3}[1-9][0-9]*)?$")]
    private static partial Regex ExcelCellReferencePattern();
}
