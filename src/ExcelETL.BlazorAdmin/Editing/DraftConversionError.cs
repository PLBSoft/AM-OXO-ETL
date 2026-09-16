namespace ExcelETL.BlazorAdmin.Editing;

// One failed conversion attempt on one draft node (Draft), carrying the real exception the Domain
// constructor threw (Exception) -- never localized here (see ConversionResult<T>'s own header
// comment for why). Draft is `object` rather than a generic type parameter because a single
// ConversionResult<T> can carry errors attached to several *different* draft types at once (e.g.
// ExportProfileDraftMapper.ConvertSheetRule can fail on a column draft, a point-column draft, or the
// rule draft itself, all in one call).
public sealed record DraftConversionError(object Draft, Exception Exception);
