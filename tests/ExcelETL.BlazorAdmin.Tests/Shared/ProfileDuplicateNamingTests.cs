using ExcelETL.BlazorAdmin.Shared;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Shared;

public class ProfileDuplicateNamingTests
{
    [Fact]
    public void BuildAvailableDuplicateName_WithNameNotAlreadyTaken_ReturnsCandidateUnchanged()
    {
        var result = ProfileDuplicateNaming.BuildAvailableDuplicateName(
            "Profil OXO standard", "(Copy)", existingNames: ["Profil OXO standard"]);

        result.Should().Be("Profil OXO standard (Copy)");
    }

    [Fact]
    public void BuildAvailableDuplicateName_WithCandidateAlreadyTaken_IncrementsSuffix()
    {
        var result = ProfileDuplicateNaming.BuildAvailableDuplicateName(
            "Profil OXO standard",
            "(Copy)",
            existingNames: ["Profil OXO standard", "Profil OXO standard (Copy)"]);

        result.Should().Be("Profil OXO standard (Copy 2)");
    }

    [Fact]
    public void BuildAvailableDuplicateName_DuplicatingAnAlreadySuffixedCopy_ConvergesOnSameBaseName()
    {
        var result = ProfileDuplicateNaming.BuildAvailableDuplicateName(
            "Profil OXO standard (Copy)",
            "(Copy)",
            existingNames: ["Profil OXO standard", "Profil OXO standard (Copy)"]);

        result.Should().Be("Profil OXO standard (Copy 2)");
    }
}
