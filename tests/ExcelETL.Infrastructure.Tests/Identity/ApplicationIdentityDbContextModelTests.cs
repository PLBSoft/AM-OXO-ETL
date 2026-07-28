using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 050 (50.5). The InMemory provider applies neither SQL Server indexes nor filter clauses --
// this only proves the *model* is configured correctly (unique index declared, filter clause
// present, 50-char max lengths). The index's/filter's real effect can only be verified against a
// real SQL Server (the essai a blanc mentioned in the ticket) -- not claimed here.
public class ApplicationIdentityDbContextModelTests
{
    private static IModel BuildModel()
    {
        using var context = new ApplicationIdentityDbContext(
            new DbContextOptionsBuilder<ApplicationIdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        return context.Model;
    }

    [Fact]
    public void NormalizedEmailIndex_IsUnique_WithNotNullFilter()
    {
        var entityType = BuildModel().FindEntityType(typeof(ApplicationUser))!;
        var index = entityType.GetIndexes().Single(i => i.GetDatabaseName() == "EmailIndex");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("[NormalizedEmail] IS NOT NULL");
    }

    [Fact]
    public void FirstName_And_LastName_HaveMaxLengthFifty()
    {
        var entityType = BuildModel().FindEntityType(typeof(ApplicationUser))!;

        entityType.FindProperty(nameof(ApplicationUser.FirstName))!.GetMaxLength().Should().Be(50);
        entityType.FindProperty(nameof(ApplicationUser.LastName))!.GetMaxLength().Should().Be(50);
    }
}
