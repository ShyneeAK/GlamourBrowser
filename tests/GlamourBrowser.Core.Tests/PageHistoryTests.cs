using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class PageHistoryTests
{
    private static TileKey Key(uint id) => new(id, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose);

    private static IReadOnlyList<TileKey> Page(params uint[] ids) => ids.Select(Key).ToList();

    [Fact]
    public void Before_ListsTheEarlierPagesMostRecentFirst()
    {
        var history = new PageHistory();
        history.Show(Page(1, 2));
        history.Show(Page(3, 4));
        history.Show(Page(5, 6));

        Assert.Equal(Page(3, 4, 1, 2), history.Before(2).ToList());
        Assert.Equal(Page(3, 4), history.Before(1).ToList());
    }

    [Fact]
    public void Before_Zero_IsEmpty()
    {
        var history = new PageHistory();
        history.Show(Page(1));
        history.Show(Page(2));

        Assert.Empty(history.Before(0));
    }

    [Fact]
    public void Show_APageShownBefore_MovesToTheFront()
    {
        var history = new PageHistory();
        history.Show(Page(1));
        history.Show(Page(2));
        history.Show(Page(1));

        Assert.Equal(Page(2), history.Before(5).ToList());
    }

    [Fact]
    public void Show_TheCurrentPageAgain_KeepsTheHistory()
    {
        var history = new PageHistory();
        history.Show(Page(1));
        history.Show(Page(2));
        history.Show(Page(2));

        Assert.Equal(Page(1), history.Before(5).ToList());
    }

    [Fact]
    public void Show_AnEmptyPage_KeepsTheLastPageButIsNotKeptItself()
    {
        var history = new PageHistory();
        history.Show(Page(1));
        history.Show(Page());

        Assert.Equal(Page(1), history.Before(1).ToList());

        history.Show(Page(2));
        Assert.Equal(Page(1), history.Before(5).ToList());
    }

    [Fact]
    public void MaxPages_AllowsAHundredPagesInTheCache()
        => Assert.Equal(100, PageHistory.MaxPages);

    [Fact]
    public void Show_KeepsAtMostMaxPagesBeforeTheCurrent()
    {
        var history = new PageHistory();
        for (var i = 1u; i <= PageHistory.MaxPages + 10; i++)
            history.Show(Page(i));

        var before = history.Before(PageHistory.MaxPages + 10).ToList();
        Assert.Equal(PageHistory.MaxPages, before.Count);
        Assert.Equal(Key(PageHistory.MaxPages + 9), before[0]);
    }

    [Fact]
    public void Clear_ForgetsAllPages()
    {
        var history = new PageHistory();
        history.Show(Page(1));
        history.Show(Page(2));
        history.Clear();
        history.Show(Page(3));

        Assert.Empty(history.Before(5));
    }
}
