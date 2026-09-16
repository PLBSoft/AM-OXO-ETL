using ExcelETL.BlazorAdmin.Editing;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing;

// Lot 075.1 (docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md): the single
// "which item of this list is open for editing, and what did it look like before" state reused by
// every editable list of the import editor, instead of one index + one snapshot field per list.
public class ListItemEditStateTests
{
    private sealed class ItemDraft
    {
        public string Value { get; set; } = string.Empty;
    }

    private static List<ItemDraft> List(params string[] values) => [.. values.Select(v => new ItemDraft { Value = v })];

    [Fact]
    public void NewState_HasNothingOpen()
    {
        var state = new ListItemEditState<ItemDraft>();

        state.Index.Should().BeNull();
        state.IsOpen(0).Should().BeFalse();
    }

    [Fact]
    public void Open_MarksTheIndexOpen()
    {
        var list = List("a", "b");
        var state = new ListItemEditState<ItemDraft>();

        state.Open(list, 1);

        state.Index.Should().Be(1);
        state.IsOpen(1).Should().BeTrue();
        state.IsOpen(0).Should().BeFalse();
    }

    [Fact]
    public void Cancel_RestoresTheValueTheItemHadWhenOpened_AndClosesIt()
    {
        var list = List("a", "b");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 1);
        list[1].Value = "modifié";

        state.Cancel(list);

        list[1].Value.Should().Be("b");
        state.Index.Should().BeNull();
    }

    [Fact]
    public void Close_KeepsTheEditedValue()
    {
        var list = List("a", "b");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 0);
        list[0].Value = "modifié";

        state.Close();

        list[0].Value.Should().Be("modifié");
        state.Index.Should().BeNull();
    }

    [Fact]
    public void Open_WhileAnotherItemIsOpen_CancelsThePreviousEditFirst()
    {
        // Pre-migration behavior: opening another item unmounted/replaced the previous edit form,
        // silently discarding its unsaved typing.
        var list = List("a", "b");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 0);
        list[0].Value = "modifié";

        state.Open(list, 1);

        list[0].Value.Should().Be("a");
        state.Index.Should().Be(1);
    }

    [Fact]
    public void Cancel_WhenNothingIsOpen_DoesNothing()
    {
        var list = List("a");
        var state = new ListItemEditState<ItemDraft>();

        state.Cancel(list);

        list[0].Value.Should().Be("a");
    }

    [Fact]
    public void Removed_TheOpenItem_ClosesTheEdit()
    {
        var list = List("a", "b");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 1);

        list.RemoveAt(1);
        state.Removed(1);

        state.Index.Should().BeNull();
    }

    [Fact]
    public void Removed_AnItemBeforeTheOpenOne_ShiftsTheOpenIndexSoItStillPointsToTheSameItem()
    {
        var list = List("a", "b", "c");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 2);

        list.RemoveAt(0);
        state.Removed(0);

        state.Index.Should().Be(1);
        list[state.Index!.Value].Value.Should().Be("c");
    }

    [Fact]
    public void Removed_AnItemAfterTheOpenOne_LeavesTheIndexUnchanged()
    {
        var list = List("a", "b", "c");
        var state = new ListItemEditState<ItemDraft>();
        state.Open(list, 0);

        list.RemoveAt(2);
        state.Removed(2);

        state.Index.Should().Be(0);
    }
}
