using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;
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
}
