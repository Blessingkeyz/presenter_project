namespace BiblePresenter.App.ViewModels;

/// <summary>Index arithmetic for moving and removing set items while one of them is live (so "roll into the next item" keeps pointing at the right one).</summary>
public static class SetReorder
{
    /// <summary>Final index of an item dragged from <paramref name="oldIndex"/> to an insertion point (0..count, measured in the list as it was before the move).</summary>
    public static int DestinationIndex(int oldIndex, int insertIndex, int count)
        => Math.Clamp(insertIndex > oldIndex ? insertIndex - 1 : insertIndex, 0, count - 1);

    /// <summary>Where the item that was at <paramref name="index"/> ends up after another item moved from <paramref name="oldIndex"/> to <paramref name="newIndex"/>.</summary>
    public static int IndexAfterMove(int index, int oldIndex, int newIndex)
    {
        if (index == oldIndex)
            return newIndex;
        if (oldIndex < index && index <= newIndex)
            return index - 1;
        if (newIndex <= index && index < oldIndex)
            return index + 1;
        return index;
    }

    /// <summary>Where the item at <paramref name="index"/> ends up after the item at <paramref name="removedIndex"/> is removed; null if it is the one removed.</summary>
    public static int? IndexAfterRemove(int index, int removedIndex)
    {
        if (index == removedIndex)
            return null;
        return removedIndex < index ? index - 1 : index;
    }
}
