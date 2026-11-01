using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Archiving;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ExcelETL.WebAPI.Controllers;

// Read-only history of files archived by OxoController.Process (Lot 034) -- lets an M2M caller
// look up what was generated for a given equipement/repere, and re-download the archived
// source/target bytes, without going through BlazorAdmin's own /generated-files admin page.
[ApiController]
[Route("api/generated-files")]
public class GeneratedFilesController(
    IGeneratedFileArchiveStore archiveStore, IOptions<GeneratedFilesArchiveOptions> archiveOptions)
    : ControllerBase
{
    private const string WorkbookContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string PdfContentType = "application/pdf";

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? equipementRepere, CancellationToken cancellationToken)
    {
        var records = await archiveStore.SearchAsync(equipementRepere, cancellationToken);
        return Ok(records.Select(ToSummary));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var record = await archiveStore.GetByIdAsync(id, cancellationToken);
        return record is null ? NotFound() : Ok(ToSummary(record));
    }

    [HttpGet("{id:guid}/source")]
    public async Task<IActionResult> DownloadSource(Guid id, CancellationToken cancellationToken)
    {
        var record = await archiveStore.GetByIdAsync(id, cancellationToken);
        return record is null
            ? NotFound()
            : DownloadFile(record.SourceFilePath, record.SourceFileName);
    }

    // The archived source file is always present (GeneratedFileRecord's own constructor guards
    // SourceFileName/SourceFilePath as non-blank), but the target isn't: a Rejected record (the
    // pipeline never got to generation) has TargetFilePath/TargetFileName both null -- there is
    // nothing to download, so this is a plain 404, not a 500/empty-body response.
    [HttpGet("{id:guid}/target")]
    public async Task<IActionResult> DownloadTarget(Guid id, CancellationToken cancellationToken)
    {
        var record = await archiveStore.GetByIdAsync(id, cancellationToken);
        if (record is null || record.TargetFilePath is null || record.TargetFileName is null)
        {
            return NotFound();
        }

        return DownloadFile(record.TargetFilePath, record.TargetFileName);
    }

    // Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, D14): the plan pages printed to PDF,
    // one PDF per page, to be attached to the parent equipment by the caller.
    [HttpGet("{id:guid}/plans")]
    public async Task<IActionResult> ListPlans(Guid id, CancellationToken cancellationToken)
    {
        var record = await archiveStore.GetByIdAsync(id, cancellationToken);
        return record is null ? NotFound() : Ok(ToPlanResponses(record));
    }

    // index: the plan's position in the list returned above (0-based). 404 for an unknown index, or once
    // the retention purge has removed the file.
    [HttpGet("{id:guid}/plans/{index:int}")]
    public async Task<IActionResult> DownloadPlan(Guid id, int index, CancellationToken cancellationToken)
    {
        var record = await archiveStore.GetByIdAsync(id, cancellationToken);
        List<GeneratedFilePlan> plans = record is null ? [] : OrderedPlans(record);
        if (index < 0 || index >= plans.Count)
        {
            return NotFound();
        }

        return DownloadFile(plans[index].FilePath, plans[index].FileName, PdfContentType);
    }

    // Owned rows carry no position of their own: a fixed order keeps an index stable between the list
    // and the download.
    private static List<GeneratedFilePlan> OrderedPlans(GeneratedFileRecord record) =>
        [.. record.Plans.OrderBy(p => p.SheetName, StringComparer.Ordinal).ThenBy(p => p.PageNumber)];

    private List<GeneratedFilePlanResponse> ToPlanResponses(GeneratedFileRecord record) =>
        [.. OrderedPlans(record).Select((p, i) => new GeneratedFilePlanResponse(
            i, p.SheetName, p.PageNumber, p.FileName, p.SizeBytes, IsAvailable(record, p.FilePath),
            $"/api/generated-files/{record.Id}/plans/{i}"))];

    // The archive has no purge policy today, but a file could still be missing on disk (manual
    // cleanup, moved storage, etc.) -- treated as a plain 404 rather than an unqualified 500, same
    // "expected absence, not a server error" reasoning as the Rejected-record case above.
    //
    // relativePath is exactly what IGeneratedFileWriter returns and GeneratedFileRecord stores --
    // relative to the configured archive root, never an absolute path (see IGeneratedFileWriter's
    // own doc comment). Must be joined with GeneratedFilesArchive:RootPath before touching the
    // filesystem, same as BlazorAdmin's GeneratedFiles.razor already does -- File.Exists/OpenRead
    // on the bare relative path resolves against the process's working directory, not the archive
    // root, and would silently 404 every real download.
    private IActionResult DownloadFile(string relativePath, string fileName, string contentType = WorkbookContentType)
    {
        var fullPath = Path.Combine(archiveOptions.Value.RootPath, relativePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return new NotFoundResult();
        }

        var stream = System.IO.File.OpenRead(fullPath);
        return new FileStreamResult(stream, contentType) { FileDownloadName = fileName };
    }

    // Lot 090: a file is available when the retention purge has not removed it AND it is still on disk
    // (it can also vanish for other reasons) -- same existence check the download routes make.
    private bool IsAvailable(GeneratedFileRecord record, string? relativePath) =>
        record.FilesPurgedAtUtc is null
        && relativePath is not null
        && System.IO.File.Exists(Path.Combine(archiveOptions.Value.RootPath, relativePath));

    private GeneratedFileSummaryResponse ToSummary(GeneratedFileRecord record) => new(
        record.Id,
        record.GeneratedAtUtc,
        record.EquipementRepere,
        record.SourceFileName,
        record.TargetFileName,
        record.ImportProfileId,
        record.ExportProfileId,
        record.Status.ToString(),
        $"/api/generated-files/{record.Id}/source",
        record.TargetFileName is null ? null : $"/api/generated-files/{record.Id}/target",
        record.Username,
        record.IsolementCount,
        record.PointCount,
        record.TacheMultipleCount,
        record.Warnings.Count,
        record.Warnings
            .Select(w => new GeneratedFileWarningResponse(w.Sheet, w.BlockIdentifier, w.Code, w.Message, w.ExtractedValue))
            .ToList(),
        IsAvailable(record, record.SourceFilePath),
        IsAvailable(record, record.TargetFilePath),
        record.Origin,
        ToPlanResponses(record));
}
