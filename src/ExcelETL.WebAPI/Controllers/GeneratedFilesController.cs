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
    private IActionResult DownloadFile(string relativePath, string fileName)
    {
        var fullPath = Path.Combine(archiveOptions.Value.RootPath, relativePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return new NotFoundResult();
        }

        var stream = System.IO.File.OpenRead(fullPath);
        return new FileStreamResult(stream, WorkbookContentType) { FileDownloadName = fileName };
    }

    private static GeneratedFileSummaryResponse ToSummary(GeneratedFileRecord record) => new(
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
        record.TacheMultipleCount);
}
