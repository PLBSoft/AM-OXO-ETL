using ExcelETL.Domain.Common;

namespace ExcelETL.Domain.Entities;

public class ExtractionConfig : Entity
{
    private const int MaxSheets = 5;

    private readonly List<SheetConfig> _sheets = [];

    public string Name { get; }
    public IReadOnlyCollection<SheetConfig> Sheets => _sheets.AsReadOnly();

    public ExtractionConfig(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be empty.", nameof(name));
        }

        Name = name;
    }

    public void AddSheet(SheetConfig sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (_sheets.Count >= MaxSheets)
        {
            throw new InvalidOperationException(
                $"An extraction config must produce exactly 4-5 sheets; cannot add more than {MaxSheets}.");
        }

        if (_sheets.Any(s => s.SheetIndex == sheet.SheetIndex))
        {
            throw new InvalidOperationException(
                $"A sheet with index {sheet.SheetIndex} already exists in extraction config '{Name}'.");
        }

        _sheets.Add(sheet);
    }
}
