namespace ExcelETL.Application.Identity;

public sealed record UserProfile(string Id, string? Email, string? UserName, string FirstName, string LastName);
