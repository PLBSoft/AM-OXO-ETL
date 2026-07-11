namespace ExcelETL.Application.Resources;

// Marker type only -- IStringLocalizer<DomainErrorMessages> resolves entries from
// DomainErrorMessages.resx / DomainErrorMessages.fr.resx by naming convention. Domain never
// references a localization framework: it throws with a DomainErrorCode, and this table is the
// single place (shared by WebAPI and BlazorAdmin) that maps each code to user-facing text.
public sealed class DomainErrorMessages;
