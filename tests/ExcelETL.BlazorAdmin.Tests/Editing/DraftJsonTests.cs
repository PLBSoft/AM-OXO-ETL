using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing;

// Lot 074.1 (docs/tickets/tickets-tdd-lot-074-pilote-brouillon-editeur-profil-export.md). Pure xUnit,
// no bUnit -- DraftJson has no dependency on Blazor at all.
public class DraftJsonTests
{
    private sealed class SampleDraft
    {
        public string Header { get; set; } = string.Empty;
        public string MarkValue { get; set; } = "X";
        public List<string> Items { get; set; } = [];

        [JsonIgnore]
        public string? Error { get; set; }
    }

    private sealed class SampleDraftWithDomainObject
    {
        public List<ConstantColumnDefinition> ConstantColumns { get; set; } = [];
    }

    [Fact]
    public void Clone_ProducesAnIndependentDeepCopy_MutatingTheCloneNeverTouchesTheOriginal()
    {
        var original = new SampleDraft { Header = "Repère", Items = ["a", "b"] };

        var clone = DraftJson.Clone(original);
        clone.Header = "Modifié";
        clone.Items.Add("c");

        original.Header.Should().Be("Repère");
        original.Items.Should().Equal("a", "b");
    }

    [Fact]
    public void IsPristine_ANewlyConstructedInstance_IsPristine()
    {
        DraftJson.IsPristine(new SampleDraft()).Should().BeTrue();
    }

    [Fact]
    public void IsPristine_ANonEmptyDefaultValue_IsStillPristine()
    {
        // MarkValue = "X" is a real, non-empty default value (mirrors PointColumnDefinitionForm's own
        // "X"/"O" pre-fills) -- IsPristine must not confuse "has a default value" with "was edited".
        var value = new SampleDraft();

        DraftJson.IsPristine(value).Should().BeTrue();
    }

    [Fact]
    public void IsPristine_WithOneFieldSet_IsNotPristine()
    {
        var value = new SampleDraft { Header = "Repère" };

        DraftJson.IsPristine(value).Should().BeFalse();
    }

    [Fact]
    public void IsPristine_WithOnlyAnItemAdded_IsNotPristine()
    {
        var value = new SampleDraft();
        value.Items.Add("a");

        DraftJson.IsPristine(value).Should().BeFalse();
    }

    [Fact]
    public void IsPristine_IgnoresTheErrorProperty()
    {
        var value = new SampleDraft { Error = "some validation failure" };

        DraftJson.IsPristine(value).Should().BeTrue();
    }

    [Fact]
    public void Clone_DoesNotCopyTheErrorProperty_SinceItIsNeverSerialized()
    {
        var original = new SampleDraft { Error = "some validation failure" };

        var clone = DraftJson.Clone(original);

        clone.Error.Should().BeNull();
    }

    // ConstantColumnDefinition (Domain) has only one public constructor, taking (header, value) --
    // both matching its own read-only property names -- so System.Text.Json's constructor-matching
    // deserialization must round-trip it correctly with no [JsonConstructor] attribute anywhere in
    // Domain. Verified directly, not assumed, per the ticket's own explicit instruction.
    [Fact]
    public void Clone_WithAnEmbeddedImmutableDomainRecord_RoundTripsItCorrectly()
    {
        var original = new SampleDraftWithDomainObject
        {
            ConstantColumns = [new ConstantColumnDefinition("SUPPRESSION", "N")],
        };

        var clone = DraftJson.Clone(original);

        clone.ConstantColumns.Should().ContainSingle();
        clone.ConstantColumns[0].Header.Should().Be("SUPPRESSION");
        clone.ConstantColumns[0].Value.Should().Be("N");
        clone.ConstantColumns[0].Should().Be(original.ConstantColumns[0]);
    }

    // Lot 075.1: shapes the import-side drafts introduce and the export pilot never exercised -- an
    // enum, a bool, an int left at zero and a nullable Guid. Verified, not assumed.
    private sealed class SampleDraftWithScalars
    {
        public Guid? Id { get; set; }
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;
        public bool Flag { get; set; }
        public int Row { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void IsPristine_ScalarDefaults_ArePristine()
    {
        DraftJson.IsPristine(new SampleDraftWithScalars()).Should().BeTrue();
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("flag")]
    [InlineData("row")]
    [InlineData("id")]
    [InlineData("value")]
    public void IsPristine_AnyScalarChanged_IsNotPristine(string changed)
    {
        var value = new SampleDraftWithScalars();
        switch (changed)
        {
            case "operator": value.Operator = ConditionOperator.NotEquals; break;
            case "flag": value.Flag = true; break;
            case "row": value.Row = 19; break;
            case "id": value.Id = Guid.NewGuid(); break;
            case "value": value.Value = "PROLOCK"; break;
        }

        DraftJson.IsPristine(value).Should().BeFalse();
    }

    [Fact]
    public void Clone_WithScalars_RoundTripsEveryValue()
    {
        var original = new SampleDraftWithScalars
        {
            Id = Guid.NewGuid(), Operator = ConditionOperator.NotEquals, Flag = true, Row = 19, Value = "PROLOCK",
        };

        DraftJson.Clone(original).Should().BeEquivalentTo(original);
    }
}
