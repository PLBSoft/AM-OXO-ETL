using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

public class TargetWorkbookFileNameBuilderTests
{
    [Fact]
    public void Build_WithRepereAndTimestamp_ReturnsExpectedFileName()
    {
        var generatedAt = new DateTime(2026, 7, 18, 14, 5, 9);

        var fileName = TargetWorkbookFileNameBuilder.Build("38-C7401", generatedAt);

        fileName.Should().Be("MAD_38-C7401_20260718140509.xlsx");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Build_WithInvalidRepere_ThrowsArgumentException(string? invalidRepere)
    {
        var act = () => TargetWorkbookFileNameBuilder.Build(invalidRepere!, DateTime.Now);

        act.Should().Throw<ArgumentException>();
    }
}
