namespace ExcelETL.BlazorAdmin.Editing;

// Which item of one draft list is open for editing, and what it looked like when it was opened, so
// "Cancel" can put it back (lot 075, docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md).
// An open item is bound directly to its list entry -- there is no separate edit buffer -- so the
// snapshot is the only thing "Cancel" needs. One instance per editable list replaces the hand-written
// index + snapshot pair each list would otherwise need.
public sealed class ListItemEditState<TDraft> where TDraft : class
{
    private TDraft? _snapshot;

    public int? Index { get; private set; }

    public bool IsOpen(int index) => Index == index;

    // Opening an item while another one is open discards the other one's unsaved typing, as the
    // pre-draft forms did when a different item's edit form replaced theirs.
    public void Open(List<TDraft> list, int index)
    {
        Cancel(list);
        Index = index;
        _snapshot = DraftJson.Clone(list[index]);
    }

    public void Cancel(List<TDraft> list)
    {
        if (Index is { } index && _snapshot is not null)
        {
            list[index] = _snapshot;
        }

        Close();
    }

    public void Close()
    {
        Index = null;
        _snapshot = null;
    }

    // Call after removing list[removedIndex], so the open index keeps pointing at the same item.
    public void Removed(int removedIndex)
    {
        if (Index == removedIndex)
        {
            Close();
        }
        else if (removedIndex < Index)
        {
            Index--;
        }
    }
}
