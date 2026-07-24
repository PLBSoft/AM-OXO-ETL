namespace ExcelETL.Application.Exceptions;

// Raised by EfImportProfileStore/EfExportProfileStore.SaveAsync when the submitted (trimmed,
// case-insensitive) Name collides with another already-persisted profile of the same type. Not a
// DomainValidationException: uniqueness depends on the Store's persisted state, not on the entity's
// own construction invariants (see ImportProfile/ExportProfile.MaxNameLength for the invariant that
// *is* a construction-time concern). Shared by both profile types -- the failure mode and the
// message shape ("a profile named 'X' already exists") are identical either way, so there is no
// value in two near-duplicate exception classes.
public sealed class ProfileNameAlreadyExistsException(string name)
    : Exception($"A profile named '{name}' already exists."), IHasApplicationErrorCode
{
    public string Name { get; } = name;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.ProfileNameAlreadyExists;

    public IReadOnlyList<object?> Args => [Name];

    public string ResourceKey => ErrorCode.ToString();
}
