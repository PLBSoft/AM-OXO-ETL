using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api")]
public class ProfilesController(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore)
    : ControllerBase
{
    [HttpGet("import-profiles")]
    public async Task<IActionResult> GetImportProfiles(CancellationToken cancellationToken)
    {
        var profiles = await importProfileStore.GetAllAsync(cancellationToken);
        return Ok(ToSummaries(profiles.Select(p => (p.Id, p.Name))));
    }

    [HttpGet("export-profiles")]
    public async Task<IActionResult> GetExportProfiles(CancellationToken cancellationToken)
    {
        var profiles = await exportProfileStore.GetAllAsync(cancellationToken);
        return Ok(ToSummaries(profiles.Select(p => (p.Id, p.Name))));
    }

    private static IReadOnlyList<ProfileSummaryResponse> ToSummaries(IEnumerable<(Guid Id, string Name)> profiles) =>
        profiles
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ProfileSummaryResponse(p.Id, p.Name))
            .ToList();
}
