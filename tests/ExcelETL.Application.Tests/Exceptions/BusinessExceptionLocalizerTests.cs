using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Resources;
using ExcelETL.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Exceptions;

public class BusinessExceptionLocalizerTests
{
    private static BusinessExceptionLocalizer CreateSut()
    {
        var domainLocalizer = new Mock<IStringLocalizer<DomainErrorMessages>>();
        domainLocalizer
            .Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] args) => new LocalizedString(name, $"domain:{name}:{string.Join(",", args)}"));

        var applicationLocalizer = new Mock<IStringLocalizer<ApplicationMessages>>();
        applicationLocalizer
            .Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] args) => new LocalizedString(name, $"application:{name}:{string.Join(",", args)}"));

        return new BusinessExceptionLocalizer(domainLocalizer.Object, applicationLocalizer.Object);
    }

    [Fact]
    public void TryLocalize_WithApplicationException_UsesApplicationLocalizer()
    {
        var sut = CreateSut();
        var exception = new ImportProfileNotFoundException(Guid.Empty);

        var result = sut.TryLocalize(exception);

        result.Should().Be($"application:ImportProfileNotFound:{Guid.Empty}");
    }

    [Fact]
    public void TryLocalize_WithDomainException_UsesDomainLocalizer()
    {
        var sut = CreateSut();
        var exception = new DomainRuleViolationException("irrelevant", DomainErrorCode.SheetGenerationRule_DuplicateHeader, 5);

        var result = sut.TryLocalize(exception);

        result.Should().Be("domain:SheetGenerationRule_DuplicateHeader:5");
    }

    [Fact]
    public void TryLocalize_WithUnrelatedException_ReturnsNull()
    {
        var sut = CreateSut();

        var result = sut.TryLocalize(new InvalidOperationException("plain, no error code"));

        result.Should().BeNull();
    }
}
