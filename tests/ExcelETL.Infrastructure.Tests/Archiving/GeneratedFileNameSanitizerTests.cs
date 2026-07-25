using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Archiving;

public class GeneratedFileNameSanitizerTests
{
    [Theory]
    [InlineData("dossier:test.xlsx", "dossier_test.xlsx")]
    [InlineData("a\\b/c:d*e?f\"g<h>i|j.xlsx", "a_b_c_d_e_f_g_h_i_j.xlsx")]
    [InlineData("Dossier_C7401.xlsx", "Dossier_C7401.xlsx")]
    public void Sanitize_ReplacesForbiddenWindowsCharactersWithUnderscore(string rawFileName, string expected)
    {
        GeneratedFileNameSanitizer.Sanitize(rawFileName).Should().Be(expected);
    }
}
