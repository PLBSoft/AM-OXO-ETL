using System.Reflection;

namespace ExcelETL.BlazorAdmin.Services;

// Lot 062 (62.2): reads the Version/BuildDate baked into this assembly by the ExcelETL.BlazorAdmin.csproj
// version-counter target (Lot 062, 62.1), which only fires on a real Publish. Registered AddSingleton
// and read once per process -- Assembly.GetExecutingAssembly()'s attributes never change at runtime.
public sealed class ApplicationBuildInfo
{
    private const string BuildDateMetadataKey = "BuildDate";

    public ApplicationBuildInfo() : this(Assembly.GetExecutingAssembly())
    {
    }

    // Internal constructor so tests can pass a fixture assembly instead of this one, per the
    // ticket's own explicit test requirement (an assembly with/without the BuildDate metadata).
    internal ApplicationBuildInfo(Assembly assembly)
    {
        Version = ResolveVersion(assembly);
        BuildDateUtc = ResolveBuildDateUtc(assembly);
    }

    public string Version { get; }

    public DateTime? BuildDateUtc { get; }

    private static string ResolveVersion(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // AssemblyInformationalVersion carries a "+<commit sha>" suffix on a deterministic/
            // SourceLink-enabled build (confirmed empirically during 62.0/62.1) -- not meaningful to
            // a non-technical client, so only the version part before it is kept.
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    private static DateTime? ResolveBuildDateUtc(Assembly assembly)
    {
        var rawValue = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == BuildDateMetadataKey)
            ?.Value;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return DateTime.TryParse(
            rawValue,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }
}
