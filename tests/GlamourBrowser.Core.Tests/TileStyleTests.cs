using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileStyleTests
{
    [Fact]
    public void Owned_GreenFrameWithBadge()
    {
        var frame = TileStyle.FrameFor(owned: true, selected: false);
        Assert.Equal(TileStyle.OwnedColor, frame.Color);
        Assert.Equal(2f, frame.Thickness);
        Assert.True(frame.OwnedBadge);
    }

    [Fact]
    public void Missing_ThinGrayFrameWithoutBadge()
    {
        var frame = TileStyle.FrameFor(owned: false, selected: false);
        Assert.Equal(TileStyle.MissingColor, frame.Color);
        Assert.Equal(1f, frame.Thickness);
        Assert.False(frame.OwnedBadge);
    }

    [Fact]
    public void Selected_WhiteFrameKeepsBadge()
    {
        var frame = TileStyle.FrameFor(owned: true, selected: true);
        Assert.Equal(TileStyle.SelectedColor, frame.Color);
        Assert.Equal(3f, frame.Thickness);
        Assert.True(frame.OwnedBadge);
    }
}
