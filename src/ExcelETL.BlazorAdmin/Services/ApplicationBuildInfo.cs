using System.Reflection;
using ExcelETL.Hosting;

namespace ExcelETL.BlazorAdmin.Services;

// Lot 062 (62.2): reads the Version/BuildDate baked into this assembly by the ExcelETL.BlazorAdmin.csproj
// version-counter target (Lot 062, 62.1), which only fires on a real Publish. Registered AddSingleton
// and read once per process -- Assembly.GetExecutingAssembly()'s attributes never change at runtime.
// Version-parsing logic is shared via AssemblyVersionResolver in ExcelETL.Hosting -- only the counter
// value stays independent from ExcelETL.WebAPI's own ApiBuildInfo.
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
        Version = AssemblyVersionResolver.Resolve(assembly);
        BuildDateUtc = ResolveBuildDateUtc(assembly);
    }

    public string Version { get; }

    public DateTime? BuildDateUtc { get; }

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
