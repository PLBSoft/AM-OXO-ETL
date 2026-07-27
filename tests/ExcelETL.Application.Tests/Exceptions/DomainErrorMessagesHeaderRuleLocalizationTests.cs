using System.Globalization;
using System.Reflection;
using System.Resources;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Resources;
using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Exceptions;

// Lot 048 (48.7b): DomainErrorMessages.resx/.fr.resx had zero entries for any of the 7 DomainErrorCode
// members the Lot 047 header-rule types (HeaderFieldRule/HeaderCompositeRule/DirectCell/
// SheetExtractionRule's cross-validation) can throw -- IStringLocalizer silently falls back to the
// raw resource-key string when a key is missing, so the user would have seen literal text like
// "HeaderFieldRule_EmptyName" on screen. This exercises the real .resx-backed resource tables (not a
// mocked IStringLocalizer, unlike BusinessExceptionLocalizerTests) so a future forgotten key is
// actually caught.
public class DomainErrorMessagesHeaderRuleLocalizationTests
{
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

    public static IEnumerable<object[]> ResourceKeyAndTriggeringAction()
    {
        yield return
        [
            "HeaderFieldRule_EmptyName",
            () => new HeaderFieldRule(string.Empty, new DirectCell("PROCEDURE", "M2:O2")),
        ];
        yield return
        [
            "HeaderFieldRule_BlankDateFormat",
            () => new HeaderFieldRule("dateRev", new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "   "),
        ];
        yield return
        [
            "HeaderCompositeRule_EmptyName",
            () => new HeaderCompositeRule(string.Empty, "Rév {revision}"),
        ];
        yield return
        [
            "HeaderCompositeRule_EmptyTemplate",
            () => new HeaderCompositeRule("Designation", string.Empty),
        ];
        yield return
        [
            "DirectCell_EmptySheet",
            () => new DirectCell(string.Empty, "M2:O2"),
        ];
        yield return
        [
            "DirectCell_InvalidRange",
            () => new DirectCell("PROCEDURE", "not-a-range"),
        ];
    }

    [Theory]
    [MemberData(nameof(ResourceKeyAndTriggeringAction))]
    public void TryLocalize_ForEachHeaderRuleErrorCode_ReturnsMessageDifferentFromTheRawKey_InEnglish(
        string resourceKey, Func<object> triggeringAction) =>
        AssertLocalizedMessageDiffersFromKey(resourceKey, triggeringAction, "en");

    [Theory]
    [MemberData(nameof(ResourceKeyAndTriggeringAction))]
    public void TryLocalize_ForEachHeaderRuleErrorCode_ReturnsMessageDifferentFromTheRawKey_InFrench(
        string resourceKey, Func<object> triggeringAction) =>
        AssertLocalizedMessageDiffersFromKey(resourceKey, triggeringAction, "fr");

    [Fact]
    public void TryLocalize_SheetExtractionRuleWithUnknownPlaceholder_ReturnsMessageDifferentFromTheRawKey_InEnglish() =>
        AssertLocalizedMessageDiffersFromKey(
            "SheetExtractionRule_HeaderCompositeReferencesUnknownField", BuildRuleWithUnknownPlaceholder, "en");

    [Fact]
    public void TryLocalize_SheetExtractionRuleWithUnknownPlaceholder_ReturnsMessageDifferentFromTheRawKey_InFrench() =>
        AssertLocalizedMessageDiffersFromKey(
            "SheetExtractionRule_HeaderCompositeReferencesUnknownField", BuildRuleWithUnknownPlaceholder, "fr");

    private static object BuildRuleWithUnknownPlaceholder()
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);

        return new SheetExtractionRule(
            "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [],
            headerFields: [],
            headerComposites: [new HeaderCompositeRule("Designation", "Rév {inconnu}")]);
    }

    private static void AssertLocalizedMessageDiffersFromKey(string resourceKey, Func<object> triggeringAction, string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            var sut = CreateSut();

            var exception = Record.Exception(() => triggeringAction());

            exception.Should().NotBeNull();
            var message = sut.TryLocalize(exception!);

            message.Should().NotBeNull();
            message.Should().NotBe(resourceKey);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    // Real .resx-backed IStringLocalizer -- deliberately not Microsoft.Extensions.Localization's own
    // ResourceManagerStringLocalizer (that concrete implementation lives in an ASP.NET Core package
    // this test project doesn't reference, per the Application layer's Abstractions-only rule). This
    // is a thin wrapper over the plain BCL ResourceManager, resolving against CurrentUICulture exactly
    // like the real localizer does.
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
