using ExcelETL.Domain.Common;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Generation.Profile;

// An Application column of a generated sheet -- modeled on PointColumnDefinition, but distinct: an
// Application (legacy EF6 AMProgress BaseElement<->Application many-to-many, see
// docs/tickets-tdd-pivot-tableaux-applications-export.md) is not a Point/Colonne in the legacy sense.
// ApplicationNom matches EquipementPivot.Applications/IsolementPivot.Applications (trimmed,
// case-insensitive, see SheetGenerationEngine) to decide whether MarkValue is written for a given row.
public sealed record ApplicationColumnDefinition
{
    public const string DefaultMarkValue = ColumnMarking.DefaultMarkValue;

    public string ApplicationNom { get; }
    public string Header { get; }
    public string MarkValue { get; }

    public ApplicationColumnDefinition(string applicationNom, string header, string markValue = DefaultMarkValue)
    {
        if (string.IsNullOrWhiteSpace(applicationNom))
        {
            throw new DomainValidationException(
                "Application nom must not be empty.", nameof(applicationNom),
                DomainErrorCode.ApplicationColumnDefinition_EmptyApplicationNom);
        }

        if (string.IsNullOrWhiteSpace(header))
        {
            throw new DomainValidationException(
                "Header must not be empty.", nameof(header), DomainErrorCode.ApplicationColumnDefinition_EmptyHeader);
        }

        if (string.IsNullOrWhiteSpace(markValue))
        {
            throw new DomainValidationException(
                "Mark value must not be empty.", nameof(markValue),
                DomainErrorCode.ApplicationColumnDefinition_EmptyMarkValue);
        }

        ApplicationNom = applicationNom;
        Header = header;
        MarkValue = markValue;
    }
}
