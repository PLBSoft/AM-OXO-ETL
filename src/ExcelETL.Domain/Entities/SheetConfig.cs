using ExcelETL.Domain.Common;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Entities;

public class SheetConfig : Entity
{
    private readonly List<CellMapping> _cellMappings = [];

    public string SheetName { get; }
    public int SheetIndex { get; }
    public IReadOnlyCollection<CellMapping> CellMappings => _cellMappings.AsReadOnly();

    public SheetConfig(string sheetName, int sheetIndex)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new DomainValidationException(
                "Sheet name must not be empty.", nameof(sheetName), DomainErrorCode.SheetConfig_EmptySheetName);
        }

        if (sheetIndex < 0)
        {
            throw new DomainArgumentOutOfRangeException(
                nameof(sheetIndex), sheetIndex, "Sheet index must not be negative.",
                DomainErrorCode.SheetConfig_NegativeSheetIndex);
        }

        SheetName = sheetName;
        SheetIndex = sheetIndex;
    }

    public void AddCellMapping(CellMapping cellMapping)
    {
        ArgumentNullException.ThrowIfNull(cellMapping);

        if (_cellMappings.Any(m => m.TargetPropertyName == cellMapping.TargetPropertyName))
        {
            throw new DomainRuleViolationException(
                $"A cell mapping targeting property '{cellMapping.TargetPropertyName}' already exists on sheet '{SheetName}'.",
                DomainErrorCode.SheetConfig_DuplicateCellMapping,
                cellMapping.TargetPropertyName, SheetName);
        }

        _cellMappings.Add(cellMapping);
    }
}
