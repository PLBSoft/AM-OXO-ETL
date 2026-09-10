using System.Reflection;
using System.Reflection.Emit;
using ExcelETL.WebAPI.Services;
using FluentAssertions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Services;

// Version parsing itself (informational-version +suffix trimming, fallback to AssemblyName.Version)
// is shared logic -- see AssemblyVersionResolverTests in ExcelETL.Hosting.Tests for those cases.
// This test only proves ApiBuildInfo actually wires into that shared resolver.
public class ApiBuildInfoTests
{
    [Fact]
    public void Version_DelegatesToAssemblyVersionResolver()
    {
        var assembly = BuildFixtureAssembly("1.0.7+abcdef1234");

        var buildInfo = new ApiBuildInfo(assembly);

        buildInfo.Version.Should().Be("1.0.7");
    }

    private static Assembly BuildFixtureAssembly(string informationalVersion)
    {
        var assemblyName = new AssemblyName("ApiBuildInfoTests.Fixture");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
        var attributeBuilder = new CustomAttributeBuilder(constructor, [informationalVersion]);
        assemblyBuilder.SetCustomAttribute(attributeBuilder);

        assemblyBuilder.DefineDynamicModule("ApiBuildInfoTests.Fixture.Module");
        return assemblyBuilder;
    }
}
