using BiblePresenter.App.ViewModels;

namespace BiblePresenter.Tests;

public class SetReorderTests
{
    [Theory]
    [InlineData(0, 3, 5, 2)] // dragged down, dropped between items 2 and 3: lands at 2 once it lifts out
    [InlineData(3, 1, 5, 1)] // dragged up
    [InlineData(2, 2, 5, 2)] // dropped where it already is
    [InlineData(2, 3, 5, 2)] // dropped just after itself: no change
    [InlineData(0, 5, 5, 4)] // dropped past the end
    [InlineData(4, 0, 5, 0)] // dropped at the very top
    public void DestinationIndex_AccountsForTheItemLeavingItsOldSlot(int oldIndex, int insertIndex, int count, int expected)
        => Assert.Equal(expected, SetReorder.DestinationIndex(oldIndex, insertIndex, count));

    [Theory]
    [InlineData(1, 1, 4, 4)] // the moved item itself
    [InlineData(2, 0, 3, 1)] // an item between the old and new spot shifts up
    [InlineData(2, 4, 1, 3)] // moved from below to above it: it shifts down
    [InlineData(0, 2, 4, 0)] // outside the moved range: unchanged
    [InlineData(5, 2, 4, 5)]
    public void IndexAfterMove_TracksTheLiveItem(int live, int oldIndex, int newIndex, int expected)
        => Assert.Equal(expected, SetReorder.IndexAfterMove(live, oldIndex, newIndex));

    [Fact]
    public void IndexAfterRemove_ShiftsOrDropsTheLiveItem()
    {
        Assert.Equal(2, SetReorder.IndexAfterRemove(3, 0));
        Assert.Equal(3, SetReorder.IndexAfterRemove(3, 5));
        Assert.Null(SetReorder.IndexAfterRemove(3, 3));
    }
}
