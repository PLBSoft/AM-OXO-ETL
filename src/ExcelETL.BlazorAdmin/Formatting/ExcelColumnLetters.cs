namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 079.2 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): 1 -> "A",
// 27 -> "AA", 16384 -> "XFD".
public static class ExcelColumnLetters
{
    public static string FromNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

        var letters = "";
        while (number > 0)
        {
            var remainder = (number - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            number = (number - 1) / 26;
        }

        return letters;
    }
}
