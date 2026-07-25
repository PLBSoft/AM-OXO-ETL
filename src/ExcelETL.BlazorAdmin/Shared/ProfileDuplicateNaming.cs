using System.Text.RegularExpressions;

namespace ExcelETL.BlazorAdmin.Shared;

// Lot 027 (27.4): resolves a name collision by auto-incrementing suffix rather than blocking or
// silently renaming -- "{Name} (Copy)" -> "{Name} (Copy 2)" -> "{Name} (Copy 3)"... Stripping an
// already-present "(Copy[ N])" suffix off the clicked profile's own name first means duplicating
// either the original or one of its existing copies always increments from the same shared base
// name -- both land on the same next-available "(Copy N)", never a nested "(Copy) (Copy)".
//
// Lot 035 (35.5): factored out of ImportProfiles.razor/ExportProfiles.razor, which had declared
// this method independently, identically. Parameterized by IReadOnlyList<string> existingNames
// rather than a concrete profile type, so it stays reusable regardless of profile kind.
public static class ProfileDuplicateNaming
{
    public static string BuildAvailableDuplicateName(
        string profileName, string duplicateSuffixText, IReadOnlyList<string> existingNames)
    {
        var innerSuffix = duplicateSuffixText.Trim().TrimStart('(').TrimEnd(')').Trim();

        var suffixPattern = new Regex($@"^(?<base>.+) \({Regex.Escape(innerSuffix)}(?: \d+)?\)$");
        var match = suffixPattern.Match(profileName);
        var baseName = match.Success ? match.Groups["base"].Value : profileName;

        var attempt = 1;
        while (true)
        {
            var candidate = attempt == 1
                ? $"{baseName} ({innerSuffix})"
                : $"{baseName} ({innerSuffix} {attempt})";

            var isTaken = existingNames.Any(
                name => string.Equals(name.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!isTaken)
            {
                return candidate;
            }

            attempt++;
        }
    }
}
