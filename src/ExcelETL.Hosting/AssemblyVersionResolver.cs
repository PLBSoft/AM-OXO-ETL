using System.Reflection;

namespace ExcelETL.Hosting;

// Shared by ExcelETL.WebAPI's ApiBuildInfo and ExcelETL.BlazorAdmin's ApplicationBuildInfo --
// identical parsing logic, deliberately factored here (define once, both hosts call it) even
// though the two hosts keep their own independent version-counter files/AssemblyMetadata targets.
// Only the counter VALUE is meant to stay independent between the two independently-deployed
// hosts (Lot 062's own decision) -- the parsing LOGIC has no reason to drift between them, and a
// future fix here (e.g. handling a pre-release suffix) now only needs to land in one place.
public static class AssemblyVersionResolver
{
    public static string Resolve(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // AssemblyInformationalVersion carries a "+<commit sha>" suffix on a deterministic/
            // SourceLink-enabled build -- not meaningful to a non-technical client or a health-check
            // payload, so only the version part before it is kept.
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
