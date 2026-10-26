using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class LookaheadTests
{
    [Fact]
    public void Pages_TheNextPagesInOrder()
        => Assert.Equal([1, 2, 3, 4], Lookahead.Pages(0, 10, 4));

    [Fact]
    public void Pages_AtTheLastPageThePagesBeforeFollow()
        => Assert.Equal([9, 7, 6, 5], Lookahead.Pages(8, 10, 4));

    [Fact]
    public void Pages_OnTheLastPageThePagesBefore()
        => Assert.Equal([8, 7, 6, 5], Lookahead.Pages(9, 10, 4));

    [Fact]
    public void Pages_PagingBackThePreviousPagesFirst()
        => Assert.Equal([4, 3, 2, 1], Lookahead.Pages(5, 10, 4, backward: true));

    [Fact]
    public void Pages_PagingBackNearTheFirstPageTheNextFollow()
        => Assert.Equal([0, 2, 3], Lookahead.Pages(1, 10, 3, backward: true));

    [Fact]
    public void Pages_NeverMoreThanThereAre()
        => Assert.Equal([1, 2], Lookahead.Pages(0, 3, 10));

    [Fact]
    public void Pages_NoneForZero()
        => Assert.Empty(Lookahead.Pages(0, 10, 0));
}
