using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileStyleTests
{
    private const uint Accent = 0xFFEF8D5B;

    [Fact]
    public void Owned_GreenFrameAndBadge()
    {
        var frame = TileStyle.FrameFor(owned: true, selected: false, Accent);
        Assert.Equal(TileStyle.OwnedColor, frame.Color);
        Assert.Equal(TileStyle.OwnedColor, frame.BadgeColor);
        Assert.Equal(2f, frame.Thickness);
    }

    [Fact]
    public void Missing_GrayFrameAndBadge()
    {
        var frame = TileStyle.FrameFor(owned: false, selected: false, Accent);
        Assert.Equal(TileStyle.MissingColor, frame.Color);
        Assert.Equal(TileStyle.MissingColor, frame.BadgeColor);
        Assert.Equal(2f, frame.Thickness);
    }

    [Fact]
    public void Selected_UsesGivenAccentAndKeepsOwnershipBadge()
    {
        Assert.Equal(new TileFrame(Accent, 3f, TileStyle.OwnedColor), TileStyle.FrameFor(owned: true, selected: true, Accent));
        Assert.Equal(new TileFrame(0xFFFF70A9, 3f, TileStyle.MissingColor), TileStyle.FrameFor(owned: false, selected: true, 0xFFFF70A9));
    }

    [Fact]
    public void ImageTint_DimsMissingOnly()
    {
        Assert.Equal(0xFFA6A6A6u, TileStyle.ImageTint(owned: false, selected: false, dim: true));
        Assert.Equal(0xFFFFFFFFu, TileStyle.ImageTint(owned: true, selected: false, dim: true));
        Assert.Equal(0xFFFFFFFFu, TileStyle.ImageTint(owned: false, selected: true, dim: true));
    }

    [Fact]
    public void ImageTint_DimmingOff_NeverTints()
        => Assert.Equal(0xFFFFFFFFu, TileStyle.ImageTint(owned: false, selected: false, dim: false));
}
