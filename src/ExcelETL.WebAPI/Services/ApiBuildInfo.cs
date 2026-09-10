using System.Reflection;
using ExcelETL.Hosting;

namespace ExcelETL.WebAPI.Services;

// Reads the Version baked into this assembly by ExcelETL.WebAPI.csproj's own version-counter
// target, which only fires on a real Publish (see the .csproj comment). Own counter, independent
// from ExcelETL.BlazorAdmin's ApplicationBuildInfo (Lot 062) -- the two hosts are deployed
// independently and were never meant to share one counter (parsing logic is shared via
// AssemblyVersionResolver in ExcelETL.Hosting; only the counter value stays independent).
// Registered AddSingleton and read once per process -- Assembly.GetExecutingAssembly()'s
// attributes never change at runtime.
public sealed class ApiBuildInfo
{
    public ApiBuildInfo() : this(Assembly.GetExecutingAssembly())
    {
    }

    // Internal constructor so tests can pass a fixture assembly instead of this one.
    internal ApiBuildInfo(Assembly assembly)
    {
        Version = AssemblyVersionResolver.Resolve(assembly);
    }

    public string Version { get; }
}
