using ExcelETL.Domain.Archiving;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Archiving;

public class GeneratedFileRecordTests
{
    [Fact]
    public void Constructor_WithSuccessCase_AssignsAllProperties()
    {
        var id = Guid.NewGuid();
        var generatedAtUtc = new DateTime(2026, 7, 25, 14, 30, 0, DateTimeKind.Utc);
        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();

        var record = new GeneratedFileRecord(
            id,
            generatedAtUtc,
            "38-C7401",
            "Dossier_C7401.xlsx",
            @"2026\07\20260725-143000-123_source_Dossier_C7401.xlsx",
            "MAD_38-C7401_20260725143000.xlsx",
            @"2026\07\20260725-143000-123_target_Dossier_C7401.xlsx",
            importProfileId,
            exportProfileId,
            GeneratedFileArchiveStatus.Success);

        record.Id.Should().Be(id);
        record.GeneratedAtUtc.Should().Be(generatedAtUtc);
        record.EquipementRepere.Should().Be("38-C7401");
        record.SourceFileName.Should().Be("Dossier_C7401.xlsx");
        record.SourceFilePath.Should().Be(@"2026\07\20260725-143000-123_source_Dossier_C7401.xlsx");
        record.TargetFileName.Should().Be("MAD_38-C7401_20260725143000.xlsx");
        record.TargetFilePath.Should().Be(@"2026\07\20260725-143000-123_target_Dossier_C7401.xlsx");
        record.ImportProfileId.Should().Be(importProfileId);
        record.ExportProfileId.Should().Be(exportProfileId);
        record.Status.Should().Be(GeneratedFileArchiveStatus.Success);
    }

    [Fact]
    public void Constructor_WithRejectedCase_AcceptsNullEquipementRepereAndTargetFields()
    {
        var act = () => new GeneratedFileRecord(
            Guid.NewGuid(),
            DateTime.UtcNow,
            equipementRepere: null,
            sourceFileName: "Dossier_corrompu.xlsx",
            sourceFilePath: @"2026\07\20260725-143000-123_source_Dossier_corrompu.xlsx",
            targetFileName: null,
            targetFilePath: null,
            importProfileId: Guid.NewGuid(),
            exportProfileId: Guid.NewGuid(),
            status: GeneratedFileArchiveStatus.Rejected);

        var record = act.Should().NotThrow().Subject;
        record.EquipementRepere.Should().BeNull();
        record.TargetFileName.Should().BeNull();
        record.TargetFilePath.Should().BeNull();
        record.Status.Should().Be(GeneratedFileArchiveStatus.Rejected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptySourceFileName_ThrowsArgumentException(string? sourceFileName)
    {
        var act = () => new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, null, sourceFileName!, "path",
            null, null, Guid.NewGuid(), null, GeneratedFileArchiveStatus.Rejected);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptySourceFilePath_ThrowsArgumentException(string? sourceFilePath)
    {
        var act = () => new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, null, "source.xlsx", sourceFilePath!,
            null, null, Guid.NewGuid(), null, GeneratedFileArchiveStatus.Rejected);

        act.Should().Throw<ArgumentException>();
    }
}
