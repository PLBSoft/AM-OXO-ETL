using ExcelETL.Application.Exceptions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Exceptions;

public class ProfileNameAlreadyExistsExceptionTests
{
    [Fact]
    public void Constructor_ExposesCollidingName()
    {
        var exception = new ProfileNameAlreadyExistsException("Profil OXO standard");

        exception.Name.Should().Be("Profil OXO standard");
    }

    [Fact]
    public void Constructor_ExposesErrorCodeAndArgsForLocalization()
    {
        var exception = new ProfileNameAlreadyExistsException("Profil OXO standard");

        exception.ErrorCode.Should().Be(ApplicationErrorCode.ProfileNameAlreadyExists);
        exception.ResourceKey.Should().Be(nameof(ApplicationErrorCode.ProfileNameAlreadyExists));
        exception.Args.Should().BeEquivalentTo(["Profil OXO standard"]);
    }
}
