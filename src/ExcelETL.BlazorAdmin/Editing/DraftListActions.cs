namespace ExcelETL.BlazorAdmin.Editing;

// The two list gestures every editable draft list repeats (lot 075): submitting one item -- an
// already-added one being edited, or the list's pending "Add" row -- and deleting one. Generic over
// the draft type; the actual validation is the caller's conversion (one of the draft mappers), so no
// business rule lives here.
public static class DraftListActions
{
    // Converts list[index], or `pending` when index is null. Failure: the item keeps what was typed and
    // carries the first error, localized; returns false. Success: its error is cleared; a pending row is
    // appended to the list and replaced (via replacePending) by a fresh instance; an edited item's edit
    // is closed; returns true.
    public static bool Submit<TDraft, TValue>(
        List<TDraft> list, int? index, TDraft pending, Action<TDraft> replacePending,
        ListItemEditState<TDraft> edit, Func<TDraft, ConversionResult<TValue>> convert, Func<Exception, string> localize)
        where TDraft : class, IDraftWithError, new()
    {
        var target = index is { } i ? list[i] : pending;
        var result = convert(target);
        if (!result.IsSuccess)
        {
            target.Error = localize(result.Errors[0].Exception);
            return false;
        }

        target.Error = null;
        if (index is null)
        {
            list.Add(target);
            replacePending(new TDraft());
        }
        else
        {
            edit.Close();
        }

        return true;
    }

    public static void Delete<TDraft>(List<TDraft> list, int index, ListItemEditState<TDraft> edit)
        where TDraft : class
    {
        list.RemoveAt(index);
        edit.Removed(index);
    }

    // Clears every Error in a draft tree before a new conversion attempt, so an error the user has since
    // fixed never stays on screen. Walks public properties generically (drafts, lists of drafts): no code
    // per draft type.
    public static void ClearErrors(object draft)
    {
        if (draft is IDraftWithError withError)
        {
            withError.Error = null;
        }

        foreach (var property in draft.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length > 0 || property.PropertyType == typeof(string))
            {
                continue;
            }

            switch (property.GetValue(draft))
            {
                case IDraftWithError child:
                    ClearErrors(child);
                    break;
                case System.Collections.IEnumerable items:
                    foreach (var item in items)
                    {
                        if (item is IDraftWithError)
                        {
                            ClearErrors(item);
                        }
                    }

                    break;
            }
        }
    }

    // Puts each conversion failure on its own draft, for the component rendering that draft to show.
    public static void ApplyErrors(IEnumerable<DraftConversionError> errors, Func<Exception, string> localize)
    {
        foreach (var error in errors)
        {
            if (error.Draft is IDraftWithError withError)
            {
                withError.Error = localize(error.Exception);
            }
        }
    }
}
