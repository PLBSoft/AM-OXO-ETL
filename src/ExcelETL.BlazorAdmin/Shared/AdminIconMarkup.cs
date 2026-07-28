namespace ExcelETL.BlazorAdmin.Shared;

// Inline SVG shapes for icon-only row actions (Modify/Duplicate/Delete), reused verbatim by
// ImportProfiles.razor and ExportProfiles.razor -- previously declared independently in both
// files (Lot V3/028), which had already caused one silent divergence (Lot 030). No bootstrap-icons
// font/CSS is loaded anywhere in this project (see NavMenu.razor.css), so these stay inline SVG,
// not `bi bi-*` classes.
public static class AdminIconMarkup
{
    public const string Pencil =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M15.502 1.94a.5.5 0 0 1 0 .706L14.459 3.69l-2-2L13.502.646a.5.5 0 0 1 .707 0zm-1.75 2.456-2-2L4.939 9.21a.5.5 0 0 0-.121.196l-.805 2.414a.25.25 0 0 0 .316.316l2.414-.805a.5.5 0 0 0 .196-.12z" /><path fill-rule="evenodd" d="M1 13.5A1.5 1.5 0 0 0 2.5 15h11a1.5 1.5 0 0 0 1.5-1.5v-6a.5.5 0 0 0-1 0v6a.5.5 0 0 1-.5.5h-11a.5.5 0 0 1-.5-.5v-11a.5.5 0 0 1 .5-.5H9a.5.5 0 0 0 0-1H2.5A1.5 1.5 0 0 0 1 2.5z" /></svg>""";

    public const string Copy =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path fill-rule="evenodd" d="M4 2a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zm2-1a1 1 0 0 0-1 1v8a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1z" /><path d="M2 5a1 1 0 0 0-1 1v8a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1v-1h1v1a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h1v1z" /></svg>""";

    public const string Trash =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5m2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5m3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0z" /><path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1H6a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1h3.5a1 1 0 0 1 1 1zM4.118 4 4 4.059V13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V4.059L13.882 4zM2.5 3V2h11v1z" /></svg>""";

    // Lot 041 (41.2): reused verbatim from NavMenu.razor.css's bi-plus-square-fill-nav-menu shape --
    // the CTA "Créer" icon (convention-ui-blazor-icones-boutons.md's own "+ Créer un profil" example).
    public const string Plus =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z" /></svg>""";

    // Lot 041 (41.2): the "Enregistrer" checkmark, already duplicated inline across SheetRuleForm's
    // own Save-changes buttons (unconditional colonne/point rule edit rows) before this lot -- moved
    // here as the single source of truth, same rationale as Pencil/Copy/Trash (Lot 035.5).
    public const string Check =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M12.736 3.97a.733.733 0 0 1 1.047 0c.286.289.29.756.01 1.05L7.88 12.01a.733.733 0 0 1-1.065.02L3.217 8.384a.757.757 0 0 1 0-1.06.733.733 0 0 1 1.047 0l3.052 3.093 5.4-6.425a.247.247 0 0 1 .02-.022z" /></svg>""";

    // Lot 041 (41.2): reused verbatim from NavMenu.razor.css's bi-send-nav-menu shape -- ApiTest.razor's
    // own process-button submits the uploaded file to the real Web API over HTTP, the same semantic
    // action the ApiTest nav link's own icon already represents.
    public const string Send =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M15.964.686a.5.5 0 0 0-.65-.65L.767 5.855H.766l-.452.18a.5.5 0 0 0-.082.887l.41.26.001.002 4.995 3.178 3.178 4.995.002.002.26.41a.5.5 0 0 0 .886-.083l6-15Zm-1.833 1.79L6.637 10.07l-.215-.338a.5.5 0 0 0-.154-.154l-.338-.215 7.594-7.594.83-.169-.169.83Z" /></svg>""";

    // Lot 041 (41.2): reused verbatim from NavMenu.razor.css's bi-file-earmark-spreadsheet-nav-menu
    // shape -- ExportProfileTest.razor's own generate-workbook-button literally generates an Excel
    // workbook, the same semantic action the Export profiles nav link's own icon already represents.
    public const string FileEarmarkSpreadsheet =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M9.293 0H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V4.707A1 1 0 0 0 13.707 4L10 .293A1 1 0 0 0 9.293 0M9.5 3.5v-2l3 3h-2a1 1 0 0 1-1-1" /><path d="M4 8h5v3H4zM4 11h5v1a1 1 0 0 0 1 1h1v-2zM10 12h1a1 1 0 0 0 1-1v-1h-2zM12 10V8h-2v2zM4 7h8V6H4z" /></svg>""";

    // Lot 044 (44.3): row action for resetting a user's password (Users.razor) -- no existing key
    // shape in this project's icon set (all reused verbatim from NavMenu.razor.css so far) to
    // borrow from, so this is a small, deliberately simple ring-plus-teeth silhouette rather than a
    // hand-transcribed complex Bootstrap Icons path (risk of a subtly malformed `d` attribute for a
    // purely decorative glyph).
    public const string Key =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><circle cx="4.5" cy="8" r="3" fill="none" stroke="currentColor" stroke-width="1.5" /><rect x="7" y="7.25" width="8" height="1.5" /><rect x="11" y="8.75" width="1.5" height="2.5" /><rect x="13" y="8.75" width="1.5" height="2" /></svg>""";

    // Lot 054 (54.3): reused verbatim from NavMenu.razor.css's bi-collection-nav-menu/
    // bi-archive-nav-menu shapes -- the home page's Import profiles/Generated files KPI tiles
    // represent the exact same thing those nav links already do.
    public const string Collection =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M2.5 3.5a.5.5 0 0 1 .5-.5h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm2-2a.5.5 0 0 1 .5-.5h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zM0 8a1.5 1.5 0 0 1 1.5-1.5h13A1.5 1.5 0 0 1 16 8v6a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 0 14zm1.5-.5A.5.5 0 0 0 1 8v6a.5.5 0 0 0 .5.5h13a.5.5 0 0 0 .5-.5V8a.5.5 0 0 0-.5-.5z" /></svg>""";

    public const string Archive =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path d="M0 2a1 1 0 0 1 1-1h14a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1v7.5a2.5 2.5 0 0 1-2.5 2.5h-9A2.5 2.5 0 0 1 1 12.5V5a1 1 0 0 1-1-1zm2 3v7.5A1.5 1.5 0 0 0 3.5 14h9a1.5 1.5 0 0 0 1.5-1.5V5zm13-3H1v2h14zM5 7.5a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5" /></svg>""";

    // Lot 054 (54.3): the home page's "last generation" tile isn't a link to an existing nav icon, so
    // no shape to reuse verbatim -- a deliberately simple, hand-drawn clock face (circle + two hands)
    // in the same style as Key, to avoid a transcription risk on a purely decorative glyph.
    public const string Clock =
        """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><circle cx="8" cy="8" r="6.25" /><path d="M8 4.5V8l3 1.75" stroke-linecap="round" stroke-linejoin="round" /></svg>""";
}
