using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileStyleTests
{
    [Fact]
    public void Owned_IsNotTinted() => Assert.Equal(TileStyle.NoTint, TileStyle.TintFor(owned: true, dimMissing: true));

    [Fact]
    public void Missing_IsTintedWhenDimmingIsOn() => Assert.Equal(TileStyle.MissingTint, TileStyle.TintFor(owned: false, dimMissing: true));

    [Fact]
    public void Missing_IsNotTintedWhenDimmingIsOff() => Assert.Equal(TileStyle.NoTint, TileStyle.TintFor(owned: false, dimMissing: false));

    [Fact]
    public void MissingTint_IsOpaqueAndDarker()
    {
        Assert.Equal(0xFFu, TileStyle.MissingTint >> 24);
        Assert.True((TileStyle.MissingTint & 0xFF) < 0xFF);
    }
}
