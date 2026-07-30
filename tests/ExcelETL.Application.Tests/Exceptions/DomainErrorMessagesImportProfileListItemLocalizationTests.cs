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

// Lot 059 (59.1): DomainErrorMessages.resx/.fr.resx had zero entries for the 6 new DomainErrorCode
// members ImportProfile's Tableau/Application-name validation can throw. Same idiom as
// DomainErrorMessagesHeaderRuleLocalizationTests -- real .resx-backed resource tables, not a mocked
// IStringLocalizer, so a forgotten key is actually caught rather than silently falling back to the
// raw resource-key string.
public class DomainErrorMessagesImportProfileListItemLocalizationTests
{
    private const string EquipementTypeElementNom = "MAD TRAVAUX";

    private static SheetExtractionRule ValidRule() => new(
        "ISOLEMENT",
        new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]),
        [],
        [],
        [],
        []);

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
            "ImportProfile_EmptyTableauName",
            () => new ImportProfile("Profil", "MAD-OXO-", EquipementTypeElementNom, [""], [], [ValidRule()]),
        ];
        yield return
        [
            "ImportProfile_TableauNameTooLong",
            () => new ImportProfile(
                "Profil", "MAD-OXO-", EquipementTypeElementNom, [new string('A', 51)], [], [ValidRule()]),
        ];
        yield return
        [
            "ImportProfile_DuplicateTableauName",
            () => new ImportProfile(
                "Profil", "MAD-OXO-", EquipementTypeElementNom, ["zzz", "ZZZ"], [], [ValidRule()]),
        ];
        yield return
        [
            "ImportProfile_EmptyApplicationName",
            () => new ImportProfile("Profil", "MAD-OXO-", EquipementTypeElementNom, [], [""], [ValidRule()]),
        ];
        yield return
        [
            "ImportProfile_ApplicationNameTooLong",
            () => new ImportProfile(
                "Profil", "MAD-OXO-", EquipementTypeElementNom, [], [new string('A', 51)], [ValidRule()]),
        ];
        yield return
        [
            "ImportProfile_DuplicateApplicationName",
            () => new ImportProfile(
                "Profil", "MAD-OXO-", EquipementTypeElementNom, [], ["PROGRESS", "progress"], [ValidRule()]),
        ];
    }

    [Theory]
    [MemberData(nameof(ResourceKeyAndTriggeringAction))]
    public void TryLocalize_ForEachListItemErrorCode_ReturnsMessageDifferentFromTheRawKey_InEnglish(
        string resourceKey, Func<object> triggeringAction) =>
        AssertLocalizedMessageDiffersFromKey(resourceKey, triggeringAction, "en");

    [Theory]
    [MemberData(nameof(ResourceKeyAndTriggeringAction))]
    public void TryLocalize_ForEachListItemErrorCode_ReturnsMessageDifferentFromTheRawKey_InFrench(
        string resourceKey, Func<object> triggeringAction) =>
        AssertLocalizedMessageDiffersFromKey(resourceKey, triggeringAction, "fr");

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

    // Real .resx-backed IStringLocalizer -- see DomainErrorMessagesHeaderRuleLocalizationTests for why
    // this isn't Microsoft.Extensions.Localization's own ResourceManagerStringLocalizer.
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
