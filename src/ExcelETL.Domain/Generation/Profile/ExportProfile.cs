using ExcelETL.Domain.Common;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Generation.Profile;

// The aggregate configuring one target-workbook generation run -- symmetric to ImportProfile, but a
// record rather than an Entity subclass: this lot's ticket explicitly asks for record (structural)
// equality across all 4 new Generation/Profile types, which would conflict with Entity's
// identity-only Equals/GetHashCode override. Id is still needed (kept as a plain property, not
// Entity-derived) because I6's EfExportProfileStore upserts by Id, mirroring ImportProfile's
// constructor-overload pattern for reconstructing an existing profile under its original Id.
public sealed record ExportProfile
{
    public const int MaxNameLength = ProfileNaming.MaxNameLength;

    // See RepeatingBlockLocator.Fields (Extraction/Primitives) for why this needs a backing field
    // instead of a plain auto-property: EF Core cannot constructor-bind an entity-collection
    // navigation.
    private readonly List<SheetGenerationRule> _sheetRules = [];

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<SheetGenerationRule> SheetRules => _sheetRules;

    public ExportProfile(string name, IReadOnlyList<SheetGenerationRule> sheetRules)
        : this(Guid.NewGuid(), name, sheetRules)
    {
    }

    // Reconstructs an existing profile under its original Id. Same rationale as
    // ImportProfile(Guid, ...): editing a profile means building a brand new instance and handing it
    // to IExportProfileStore.SaveAsync under the same Id as the profile it replaces.
    public ExportProfile(Guid id, string name, IReadOnlyList<SheetGenerationRule> sheetRules)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.ExportProfile_EmptyName);
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainValidationException(
                $"Name must not exceed {MaxNameLength} characters.", nameof(name), DomainErrorCode.ExportProfile_NameTooLong,
                MaxNameLength);
        }

        ArgumentNullException.ThrowIfNull(sheetRules);

        if (sheetRules.Count == 0)
        {
            throw new DomainValidationException(
                "Sheet rules must contain at least one rule.", nameof(sheetRules), DomainErrorCode.ExportProfile_NoSheetRules);
        }

        Id = id;
        Name = name;
        _sheetRules = [.. sheetRules];
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private ExportProfile()
    {
        Name = string.Empty;
    }

    public bool Equals(ExportProfile? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && SheetRules.SequenceEqual(other.SheetRules);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        foreach (var rule in SheetRules)
        {
            hash.Add(rule);
        }

        return hash.ToHashCode();
    }
}
