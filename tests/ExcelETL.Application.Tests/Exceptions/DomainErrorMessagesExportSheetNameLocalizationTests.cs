using System.Globalization;
using System.Reflection;
using System.Resources;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Resources;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Exceptions;

// Lot 080 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md): every DomainErrorCode the
// export sheet-name checks can throw has a real English and French entry. Same real .resx-backed approach as
// DomainErrorMessagesHeaderRuleLocalizationTests; the two cultures must also differ, so a missing French entry
// (silent fallback to the neutral resource) is caught.
public class DomainErrorMessagesExportSheetNameLocalizationTests
{
    private static SheetGenerationRule Sheet(string name, PivotSource pivotSource = PivotSource.Equipement) =>
        new(name, pivotSource, [], [], []);

    public static IEnumerable<object[]> ResourceKeyAndTriggeringAction()
    {
        yield return ["SheetGenerationRule_SheetNameTooLong", () => Sheet(new string('A', 32))];
        yield return ["SheetGenerationRule_SheetNameForbiddenCharacter", () => Sheet("A/B")];
        yield return ["SheetGenerationRule_SheetNameApostropheAtEdge", () => Sheet("'Parents")];
    }

    [Theory]
    [MemberData(nameof(ResourceKeyAndTriggeringAction))]
    public void TryLocalize_ReturnsARealMessage_DifferentInEnglishAndFrench(string resourceKey, Func<object> triggeringAction)
    {
        var exception = Record.Exception(() => triggeringAction());
        exception.Should().NotBeNull();

        var english = LocalizeIn("en", exception!);
        var french = LocalizeIn("fr", exception!);

        english.Should().NotBeNull().And.NotBe(resourceKey);
        french.Should().NotBeNull().And.NotBe(resourceKey);
        french.Should().NotBe(english);
    }

    private static string? LocalizeIn(string cultureName, Exception exception)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            return CreateSut().TryLocalize(exception);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private static BusinessExceptionLocalizer CreateSut()
    {
        var domainLocalizer = new RealResxStringLocalizer<DomainErrorMessages>(
            "ExcelETL.Application.Resources.DomainErrorMessages", typeof(DomainErrorMessages).Assembly);

        var applicationLocalizer = new Mock<IStringLocalizer<ApplicationMessages>>();
        applicationLocalizer
            .Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] args) => new LocalizedString(name, name));

        return new BusinessExceptionLocalizer(domainLocalizer, applicationLocalizer.Object);
    }

    // Same thin ResourceManager wrapper as DomainErrorMessagesHeaderRuleLocalizationTests (duplicated per file,
    // this repo's no-shared-test-helper convention).
    private sealed class RealResxStringLocalizer<T>(string baseName, Assembly assembly) : IStringLocalizer<T>
    {
        private readonly ResourceManager _resourceManager = new(baseName, assembly);

        public LocalizedString this[string name]
        {
            get
            {
                var value = _resourceManager.GetString(name, CultureInfo.CurrentUICulture);
                return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var format = _resourceManager.GetString(name, CultureInfo.CurrentUICulture);
                var value = format is null ? name : string.Format(CultureInfo.CurrentCulture, format, arguments);
                return new LocalizedString(name, value, resourceNotFound: format is null);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            throw new NotSupportedException("Not needed by these tests.");
    }
}
