using System.Reflection;
using System.Reflection.Emit;
using ExcelETL.Hosting;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Hosting.Tests;

// Shared by ExcelETL.WebAPI's ApiBuildInfo and ExcelETL.BlazorAdmin's ApplicationBuildInfo --
// tested once here rather than duplicated in both hosts' own test projects.
public class AssemblyVersionResolverTests
{
    [Fact]
    public void Resolve_WithInformationalVersionAttribute_ReturnsPartBeforePlusSuffix()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: "1.0.7+abcdef1234");

        AssemblyVersionResolver.Resolve(assembly).Should().Be("1.0.7");
    }

    [Fact]
    public void Resolve_WithInformationalVersionAttributeAndNoPlusSuffix_ReturnsItVerbatim()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: "1.0.7");

        AssemblyVersionResolver.Resolve(assembly).Should().Be("1.0.7");
    }

    [Fact]
    public void Resolve_WithoutInformationalVersionAttribute_FallsBackToAssemblyNameVersion()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: null);

        AssemblyVersionResolver.Resolve(assembly).Should().Be(assembly.GetName().Version!.ToString());
    }

    private static Assembly BuildFixtureAssembly(string? informationalVersion)
    {
        var assemblyName = new AssemblyName("AssemblyVersionResolverTests.Fixture");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            var attributeBuilder = new CustomAttributeBuilder(constructor, [informationalVersion]);
            assemblyBuilder.SetCustomAttribute(attributeBuilder);
        }

        assemblyBuilder.DefineDynamicModule("AssemblyVersionResolverTests.Fixture.Module");
        return assemblyBuilder;
    }
}
