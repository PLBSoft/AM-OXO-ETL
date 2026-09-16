using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing;

// Lot 075 (docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md).
public class DraftListActionsTests
{
    private sealed class ItemDraft : IDraftWithError
    {
        public string Value { get; set; } = string.Empty;

        [JsonIgnore]
        public string? Error { get; set; }
    }

    private static ConversionResult<string> Convert(ItemDraft draft) =>
        string.IsNullOrWhiteSpace(draft.Value)
            ? ConversionResult<string>.Failure(draft, new InvalidOperationException("vide"))
            : ConversionResult<string>.Success(draft.Value);

    private static string Localize(Exception exception) => "localisé : " + exception.Message;

    [Fact]
    public void Submit_ValidPendingRow_IsAppendedAndReplacedByAFreshInstance()
    {
        var list = new List<ItemDraft> { new() { Value = "a" } };
        var pending = new ItemDraft { Value = "b", Error = "ancienne erreur" };
        ItemDraft? replacement = null;

        var ok = DraftListActions.Submit(list, null, pending, p => replacement = p, new ListItemEditState<ItemDraft>(), Convert, Localize);

        ok.Should().BeTrue();
        list.Select(i => i.Value).Should().Equal("a", "b");
        pending.Error.Should().BeNull();
        replacement.Should().NotBeNull().And.NotBeSameAs(pending);
        replacement!.Value.Should().BeEmpty();
    }

    [Fact]
    public void Submit_InvalidPendingRow_KeepsItsInputAndCarriesTheLocalizedError()
    {
        var list = new List<ItemDraft>();
        var pending = new ItemDraft { Value = " " };
        var replaced = false;

        var ok = DraftListActions.Submit(list, null, pending, _ => replaced = true, new ListItemEditState<ItemDraft>(), Convert, Localize);

        ok.Should().BeFalse();
        list.Should().BeEmpty();
        replaced.Should().BeFalse();
        pending.Value.Should().Be(" ");
        pending.Error.Should().Be("localisé : vide");
    }

    [Fact]
    public void Submit_ValidEditedItem_ClosesItsEdit()
    {
        var list = new List<ItemDraft> { new() { Value = "a" } };
        var edit = new ListItemEditState<ItemDraft>();
        edit.Open(list, 0);
        list[0].Value = "modifié";

        var ok = DraftListActions.Submit(list, 0, new ItemDraft(), _ => { }, edit, Convert, Localize);

        ok.Should().BeTrue();
        edit.Index.Should().BeNull();
        list.Should().ContainSingle().Which.Value.Should().Be("modifié");
    }

    [Fact]
    public void Submit_InvalidEditedItem_StaysOpen()
    {
        var list = new List<ItemDraft> { new() { Value = "a" } };
        var edit = new ListItemEditState<ItemDraft>();
        edit.Open(list, 0);
        list[0].Value = "";

        var ok = DraftListActions.Submit(list, 0, new ItemDraft(), _ => { }, edit, Convert, Localize);

        ok.Should().BeFalse();
        edit.Index.Should().Be(0);
        list[0].Error.Should().Be("localisé : vide");
    }

    [Fact]
    public void Delete_RemovesTheItemAndKeepsTheOpenEditPointingAtTheSameItem()
    {
        var list = new List<ItemDraft> { new() { Value = "a" }, new() { Value = "b" } };
        var edit = new ListItemEditState<ItemDraft>();
        edit.Open(list, 1);

        DraftListActions.Delete(list, 0, edit);

        list.Should().ContainSingle().Which.Value.Should().Be("b");
        edit.Index.Should().Be(0);
    }

    private sealed class TreeDraft : IDraftWithError
    {
        public string Name { get; set; } = string.Empty;
        public ItemDraft Pending { get; set; } = new();
        public List<ItemDraft> Items { get; set; } = [];

        [JsonIgnore]
        public string? Error { get; set; }
    }

    [Fact]
    public void ClearErrors_ClearsTheErrorOfEveryDraftInTheTree()
    {
        var tree = new TreeDraft
        {
            Error = "racine",
            Pending = new ItemDraft { Error = "ligne en attente" },
            Items = [new ItemDraft { Error = "élément" }],
        };

        DraftListActions.ClearErrors(tree);

        tree.Error.Should().BeNull();
        tree.Pending.Error.Should().BeNull();
        tree.Items[0].Error.Should().BeNull();
    }

    [Fact]
    public void ApplyErrors_SetsEachLocalizedErrorOnItsOwnDraft()
    {
        var first = new ItemDraft();
        var second = new ItemDraft();

        DraftListActions.ApplyErrors(
            [new DraftConversionError(first, new InvalidOperationException("un")), new DraftConversionError(second, new InvalidOperationException("deux"))],
            Localize);

        first.Error.Should().Be("localisé : un");
        second.Error.Should().Be("localisé : deux");
    }
}
