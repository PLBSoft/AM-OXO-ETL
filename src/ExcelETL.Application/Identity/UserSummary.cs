namespace ExcelETL.Application.Identity;

// Lot 050 (50.7/50.8, D6): FirstName/LastName let the list render both without a per-row
// GetByIdAsync round trip.
public sealed record UserSummary(string Id, string? Email, string? UserName, string FirstName, string LastName);
