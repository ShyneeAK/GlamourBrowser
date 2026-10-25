using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class LookaheadTests
{
    [Fact]
    public void Pages_TheNextPagesInOrder()
        => Assert.Equal([1, 2, 3, 4], Lookahead.Pages(0, 10, 4));

    [Fact]
    public void Pages_StopAtTheLastPage()
        => Assert.Equal([9], Lookahead.Pages(8, 10, 4));

    [Fact]
    public void Pages_NoneOnTheLastPage()
        => Assert.Empty(Lookahead.Pages(9, 10, 4));

    [Fact]
    public void Pages_NoneForZero()
        => Assert.Empty(Lookahead.Pages(0, 10, 0));
}
