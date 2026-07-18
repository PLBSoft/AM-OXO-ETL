namespace ExcelETL.Application.Generation;

// One sheet of intermediate generation output -- the Application layer's ClosedXML-free stand-in for
// a real worksheet, per docs/tickets-tdd-ecriture-fichier-cible.md I3. Headers is row 1; Rows are the
// data rows below it, in order.
public sealed record GeneratedSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<GeneratedRow> Rows);
