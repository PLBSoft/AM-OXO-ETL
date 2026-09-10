using System.Reflection;
using System.Reflection.Emit;
using ExcelETL.WebAPI.Services;
using FluentAssertions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Services;

public class ApiBuildInfoTests
{
    [Fact]
    public void Version_WithInformationalVersionAttribute_ReturnsPartBeforePlusSuffix()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: "1.0.7+abcdef1234");

        var buildInfo = new ApiBuildInfo(assembly);

        buildInfo.Version.Should().Be("1.0.7");
    }

    [Fact]
    public void Version_WithInformationalVersionAttributeAndNoPlusSuffix_ReturnsItVerbatim()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: "1.0.7");

        var buildInfo = new ApiBuildInfo(assembly);

        buildInfo.Version.Should().Be("1.0.7");
    }

    [Fact]
    public void Version_WithoutInformationalVersionAttribute_FallsBackToAssemblyNameVersion()
    {
        var assembly = BuildFixtureAssembly(informationalVersion: null);

        var buildInfo = new ApiBuildInfo(assembly);

        buildInfo.Version.Should().Be(assembly.GetName().Version!.ToString());
    }

    private static Assembly BuildFixtureAssembly(string? informationalVersion)
    {
        var assemblyName = new AssemblyName("ApiBuildInfoTests.Fixture");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            var attributeBuilder = new CustomAttributeBuilder(constructor, [informationalVersion]);
            assemblyBuilder.SetCustomAttribute(attributeBuilder);
        }

        assemblyBuilder.DefineDynamicModule("ApiBuildInfoTests.Fixture.Module");
        return assemblyBuilder;
    }
}
