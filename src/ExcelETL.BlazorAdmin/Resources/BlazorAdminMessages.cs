namespace ExcelETL.BlazorAdmin.Resources;

// Marker type only -- IStringLocalizer<BlazorAdminMessages> resolves entries from
// BlazorAdminMessages.resx / .fr.resx by naming convention, same approach as
// ExcelETL.Application.Resources.ApplicationMessages. Holds BlazorAdmin's own UI text (labels,
// headings, buttons, table columns) -- not shared with WebAPI, so it lives in this project
// rather than in Application.
public sealed class BlazorAdminMessages;
