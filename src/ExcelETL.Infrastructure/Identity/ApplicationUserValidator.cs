using ExcelETL.Infrastructure.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace ExcelETL.Infrastructure.Identity;

// Lot 050 (50.1). Portes D1 (nom d'utilisateur, 3-30 caracteres) et D2 (Prenom/Nom, 2-50
// caracteres, aucune restriction de jeu de caracteres) via le point d'extension IUserValidator --
// jamais du code de page (convention deja en place). AllowedUserNameCharacters (D1's character
// set) is a separate, native Identity option configured in Program.cs, not duplicated here.
public class ApplicationUserValidator(IStringLocalizer<InfrastructureMessages> localizer) : IUserValidator<ApplicationUser>
{
    public const int MinUserNameLength = 3;
    public const int MaxUserNameLength = 30;
    public const int MinNameLength = 2;
    public const int MaxNameLength = 50;

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var errors = new List<IdentityError>();

        AddIfOutOfRange(errors, user.UserName, MinUserNameLength, MaxUserNameLength, "UserNameLengthInvalid");
        AddIfOutOfRange(errors, user.FirstName, MinNameLength, MaxNameLength, "FirstNameLengthInvalid");
        AddIfOutOfRange(errors, user.LastName, MinNameLength, MaxNameLength, "LastNameLengthInvalid");

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }

    private void AddIfOutOfRange(List<IdentityError> errors, string? value, int min, int max, string code)
    {
        var length = value?.Length ?? 0;
        if (length < min || length > max)
        {
            errors.Add(new IdentityError { Code = code, Description = localizer[code, min, max] });
        }
    }
}
