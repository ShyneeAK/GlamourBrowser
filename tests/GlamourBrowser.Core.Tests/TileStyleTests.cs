using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileStyleTests
{
    [Fact]
    public void Owned_GreenFrameAndBadge()
    {
        var frame = TileStyle.FrameFor(owned: true, selected: false);
        Assert.Equal(TileStyle.OwnedColor, frame.Color);
        Assert.Equal(TileStyle.OwnedColor, frame.BadgeColor);
        Assert.Equal(2f, frame.Thickness);
    }

    [Fact]
    public void Missing_GrayFrameAndBadge()
    {
        var frame = TileStyle.FrameFor(owned: false, selected: false);
        Assert.Equal(TileStyle.MissingColor, frame.Color);
        Assert.Equal(TileStyle.MissingColor, frame.BadgeColor);
        Assert.Equal(2f, frame.Thickness);
    }

    [Fact]
    public void Selected_WhiteFrameKeepsOwnershipBadge()
    {
        Assert.Equal(new TileFrame(TileStyle.SelectedColor, 3f, TileStyle.OwnedColor), TileStyle.FrameFor(owned: true, selected: true));
        Assert.Equal(new TileFrame(TileStyle.SelectedColor, 3f, TileStyle.MissingColor), TileStyle.FrameFor(owned: false, selected: true));
    }
}
