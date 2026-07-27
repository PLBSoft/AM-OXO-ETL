using System.Security.Cryptography;

namespace ExcelETL.Infrastructure.Identity;

// Lot 044 (44.1): generates a temporary password guaranteed to contain at least one uppercase,
// one lowercase, one digit and one non-alphanumeric character at a length of 12 -- satisfies
// ASP.NET Core Identity's default password-complexity policy (RequiredLength=6, RequireUppercase/
// Lowercase/Digit/NonAlphanumeric=true) with margin, confirmed at 44.0 that IdentityOptions.Password
// is never customized anywhere in this solution. Visually ambiguous characters (0/O, 1/l/I) are
// excluded since the password is meant to be read off-screen and retyped by hand (no email
// delivery, per the ticket's explicit decision).
public static class TemporaryPasswordGenerator
{
    private const string UppercaseChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijkmnopqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const string SpecialChars = "!@#$%^&*-_=+";
    private const string AllChars = UppercaseChars + LowercaseChars + DigitChars + SpecialChars;
    private const int Length = 12;

    public static string Generate()
    {
        var chars = new List<char>(Length)
        {
            PickRandomChar(UppercaseChars),
            PickRandomChar(LowercaseChars),
            PickRandomChar(DigitChars),
            PickRandomChar(SpecialChars),
        };

        while (chars.Count < Length)
        {
            chars.Add(PickRandomChar(AllChars));
        }

        Shuffle(chars);
        return new string([.. chars]);
    }

    private static char PickRandomChar(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(List<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
