using System.Reflection;

namespace ExcelETL.WebAPI.Services;

// Reads the Version baked into this assembly by ExcelETL.WebAPI.csproj's own version-counter
// target, which only fires on a real Publish (see the .csproj comment). Own counter, independent
// from ExcelETL.BlazorAdmin's ApplicationBuildInfo (Lot 062) -- the two hosts are deployed
// independently and were never meant to share one counter. Registered AddSingleton and read once
// per process -- Assembly.GetExecutingAssembly()'s attributes never change at runtime.
public sealed class ApiBuildInfo
{
    public ApiBuildInfo() : this(Assembly.GetExecutingAssembly())
    {
    }

    // Internal constructor so tests can pass a fixture assembly instead of this one.
    internal ApiBuildInfo(Assembly assembly)
    {
        Version = ResolveVersion(assembly);
    }

    public string Version { get; }

    private static string ResolveVersion(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // AssemblyInformationalVersion carries a "+<commit sha>" suffix on a deterministic/
            // SourceLink-enabled build -- not meaningful for a health-check payload, only the
            // version part before it is kept. Same convention as ApplicationBuildInfo (Lot 062).
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
