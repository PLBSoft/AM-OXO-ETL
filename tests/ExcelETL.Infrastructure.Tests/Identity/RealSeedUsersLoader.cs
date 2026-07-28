using System.Text.Json;
using ExcelETL.Infrastructure.Identity;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Reads the real AdminSeedUsers values from BlazorAdmin's own appsettings.json rather than
// hand-copying them in a test -- a test that duplicates the values stops protecting the moment the
// seed changes, exactly when it would matter (lot 050's own explicit instruction). Shared by every
// test that needs to prove a seed-conformance property (50.1's validator check, 50.4's distinct-
// emails check).
internal static class RealSeedUsersLoader
{
    public static List<AdminSeedUser> LoadRealSeedUsers()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BlazorAdminSettingsPath("appsettings.json")));
        var section = document.RootElement.GetProperty("AdminSeedUsers");

        return JsonSerializer.Deserialize<List<AdminSeedUser>>(
            section.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    private static string BlazorAdminSettingsPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
        }

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", fileName);
    }
}
