using ExcelETL.Application.Generation;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Lot 080.1 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md, D8): guard against
// drift between the domain's sheet-name rules (ExcelSheetName) and what ClosedXML really refuses. Replaces the
// lot 079 ExcelSheetNameRulesTests (BlazorAdmin), which built invalid profiles the domain now rejects: the
// workbook is built directly here, so any name can be written.
public class ExcelSheetNameWriterAgreementTests
{
    public static TheoryData<string, string[]> Cases() => new()
    {
        { "32 characters", [new string('A', 32)] },
        { "31 characters", [new string('A', 31)] },
        { "backslash", ["A\\B"] },
        { "slash", ["A/B"] },
        { "question mark", ["A?B"] },
        { "star", ["A*B"] },
        { "opening bracket", ["A[B"] },
        { "closing bracket", ["A]B"] },
        { "colon", ["A:B"] },
        { "leading apostrophe", ["'Parents"] },
        { "trailing apostrophe", ["Parents'"] },
        { "inner apostrophe", ["Parent's"] },
        { "same name", ["Parents", "Parents"] },
        { "same name ignoring case", ["Parents", "parents"] },
        { "leading and trailing spaces", [" Parents ", "Parents"] },
        { "sheet named like a task code", ["tm_proc_rel", "TM_PROC_MAD", "TM_PROC_REL"] },
        { "task code names", ["Parents", "TM_PROC_MAD", "TM_PROC_REL"] },
        { "standard layout", ["Parents", "Enfants", "TM_PROC_MAD", "TM_PROC_REL"] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void WritingTheWorkbook_FailsExactlyWhenANameIsInvalidOrTwoNamesClash(string caseName, string[] sheetNames)
    {
        var workbook = new GeneratedWorkbook([.. sheetNames.Select(name => new GeneratedSheet(name, ["Repère"], []))]);
        using var destination = new MemoryStream();
        var write = () => new ClosedXmlWorkbookWriter(NullLogger<ClosedXmlWorkbookWriter>.Instance).Write(workbook, destination);

        if (IsRejectedByTheDomainRules(sheetNames))
        {
            write.Should().Throw<ArgumentException>(caseName);
        }
        else
        {
            write.Should().NotThrow(caseName);
        }
    }

    // The theory above is only meaningful if both outcomes occur.
    [Fact]
    public void Cases_CoverBothRejectedAndAcceptedNames()
    {
        var outcomes = Cases().Select(row => IsRejectedByTheDomainRules((string[])row[1])).ToList();

        outcomes.Should().Contain(true).And.Contain(false);
    }

    private static bool IsRejectedByTheDomainRules(string[] sheetNames) =>
        sheetNames.Any(name => !ExcelSheetName.IsValid(name))
        || sheetNames.Distinct(ExcelSheetName.NameComparer).Count() != sheetNames.Length;
}
