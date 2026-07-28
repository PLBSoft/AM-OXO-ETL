using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Real .resx-backed IStringLocalizer, mirrors the precedent already established in
// DomainErrorMessagesHeaderRuleLocalizationTests (Application.Tests) -- a thin wrapper over the
// plain BCL ResourceManager rather than a mock, so a forgotten resource key is actually caught.
internal sealed class RealResxStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly ResourceManager _resourceManager = new(typeof(T).FullName!, typeof(T).Assembly);

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
