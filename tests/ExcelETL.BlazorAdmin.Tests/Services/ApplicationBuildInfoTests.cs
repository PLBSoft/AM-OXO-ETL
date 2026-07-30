using System.Reflection;
using System.Reflection.Emit;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 062 (62.2): ApplicationBuildInfo reads Assembly custom attributes -- built dynamically with
// AssemblyBuilder rather than depending on a real published assembly, so both the "BuildDate present"
// and "BuildDate absent" cases are fully self-contained and don't depend on how this test project
// itself was built.
public class ApplicationBuildInfoTests
{
    private static Assembly BuildFixtureAssembly(string? informationalVersion, string? buildDateUtcIso8601)
    {
        var assemblyName = new AssemblyName($"Lot062Fixture_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informationalVersion]));
        }

        if (buildDateUtcIso8601 is not null)
        {
            var ctor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(ctor, ["BuildDate", buildDateUtcIso8601]));
        }

        // A dynamic assembly needs at least one module to be a fully valid Assembly instance.
        assemblyBuilder.DefineDynamicModule(assemblyName.Name!);

        return assemblyBuilder;
    }

    [Fact]
    public void Constructor_WithInformationalVersionAndBuildDateMetadata_ResolvesBothNonNull()
    {
        var buildDateIso = "2026-07-30T16:49:02.0716047Z";
        var assembly = BuildFixtureAssembly("1.0.42+abc123", buildDateIso);

        var buildInfo = new ApplicationBuildInfo(assembly);

        buildInfo.Version.Should().Be("1.0.42");
        buildInfo.BuildDateUtc.Should().NotBeNull();
        buildInfo.BuildDateUtc!.Value.Should().Be(DateTime.Parse(
            buildDateIso,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public void Constructor_WithoutBuildDateMetadata_ReturnsNullBuildDateUtc_AndDoesNotThrow()
    {
        var assembly = BuildFixtureAssembly("1.0.7", buildDateUtcIso8601: null);

        var act = () => new ApplicationBuildInfo(assembly);

        act.Should().NotThrow();

        var buildInfo = new ApplicationBuildInfo(assembly);
        buildInfo.Version.Should().Be("1.0.7");
        buildInfo.BuildDateUtc.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithoutInformationalVersionAttribute_FallsBackToAssemblyNameVersion()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: null, buildDateUtcIso8601: null);

        var buildInfo = new ApplicationBuildInfo(assembly);

        buildInfo.Version.Should().Be(assembly.GetName().Version!.ToString());
    }

    [Fact]
    public void Constructor_UsingTheRealTestAssembly_HasNoBuildDateMetadata()
    {
        // The test assembly itself was never built through ExcelETL.BlazorAdmin.csproj's Lot 062
        // publish-only version-counter target -- a real, non-fabricated "absent" case.
        var buildInfo = new ApplicationBuildInfo(Assembly.GetExecutingAssembly());

        buildInfo.BuildDateUtc.Should().BeNull();
    }
}
