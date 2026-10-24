using GlamourBrowser.Core.Layout;

namespace GlamourBrowser.Core.Tests;

public class PageLayoutTests
{
    [Fact]
    public void SixByThree_HeightLimited()
    {
        var r = PageLayout.Compute(6, 3, 1200, 900, 8, 20);
        Assert.Equal(164.8f, r.TileWidth, 0.01f);
        Assert.Equal(192, r.RenderWidth);
    }

    [Fact]
    public void TwoByOne_CapsAtNative()
    {
        var r = PageLayout.Compute(2, 1, 1200, 900, 8, 20);
        Assert.Equal(528f, r.TileWidth, 0.01f);
        Assert.Equal(576, r.RenderWidth);
    }

    [Fact]
    public void TenBySix_SmallestStep()
    {
        var r = PageLayout.Compute(10, 6, 600, 400, 8, 20);
        Assert.Equal(24f, r.TileWidth, 0.01f);
        Assert.Equal(128, r.RenderWidth);
    }

    [Fact]
    public void TooSmall_YieldsZero()
    {
        var r = PageLayout.Compute(10, 6, 50, 50, 8, 20);
        Assert.Equal(0f, r.TileWidth);
        Assert.Equal(128, r.RenderWidth);
    }
}
