using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.Controllers;

// Read-only history of files archived by OxoController.Process (Lot 034) -- lets an M2M caller
// look up what was generated for a given equipement/repere without going through BlazorAdmin's
// own /generated-files admin page. Metadata only: SourceFilePath/TargetFilePath (server-local
// filesystem paths) are never returned -- downloading the actual archived bytes over HTTP is a
// separate, not-yet-requested decision.
[ApiController]
[Route("api/generated-files")]
public class GeneratedFilesController(IGeneratedFileArchiveStore archiveStore) : ControllerBase
{
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

    private static GeneratedFileSummaryResponse ToSummary(GeneratedFileRecord record) => new(
        record.Id,
        record.GeneratedAtUtc,
        record.EquipementRepere,
        record.SourceFileName,
        record.TargetFileName,
        record.ImportProfileId,
        record.ExportProfileId,
        record.Status.ToString());
}
