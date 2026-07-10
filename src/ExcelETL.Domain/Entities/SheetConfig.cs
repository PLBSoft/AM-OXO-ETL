using ExcelETL.Domain.Common;

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
            throw new ArgumentException("Sheet name must not be empty.", nameof(sheetName));
        }

        if (sheetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sheetIndex), sheetIndex, "Sheet index must not be negative.");
        }

        SheetName = sheetName;
        SheetIndex = sheetIndex;
    }

    public void AddCellMapping(CellMapping cellMapping)
    {
        ArgumentNullException.ThrowIfNull(cellMapping);

        if (_cellMappings.Any(m => m.TargetPropertyName == cellMapping.TargetPropertyName))
        {
            throw new InvalidOperationException(
                $"A cell mapping targeting property '{cellMapping.TargetPropertyName}' already exists on sheet '{SheetName}'.");
        }

        _cellMappings.Add(cellMapping);
    }
}
