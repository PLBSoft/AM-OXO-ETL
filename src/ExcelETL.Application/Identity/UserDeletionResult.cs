namespace ExcelETL.Application.Identity;

// The two deletion guard-rails (self-deletion, last remaining Admin) are refused explicitly, not
// thrown as exceptions -- Lot 044, 44.1 decision. Users.razor is expected to pre-compute both
// conditions client-side (via GetAdminUserIdsAsync) to disable the delete button proactively, but
// the service still enforces them independently as the real safety net.
public enum UserDeletionFailureReason
{
    None,
    SelfDeletion,
    LastAdminRemaining,
}

public sealed record UserDeletionResult(bool Succeeded, UserDeletionFailureReason FailureReason, IReadOnlyList<string> Errors)
{
    public static UserDeletionResult Success { get; } = new(true, UserDeletionFailureReason.None, []);

    public static UserDeletionResult Refused(UserDeletionFailureReason reason) => new(false, reason, []);

    public static UserDeletionResult Failed(IReadOnlyList<string> errors) => new(false, UserDeletionFailureReason.None, errors);
}
