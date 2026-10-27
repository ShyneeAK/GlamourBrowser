using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileDyeOverridesTests
{
    private static readonly DyePair Black = new(81, 0);
    private static readonly DyePair Red = new(20, 0);

    [Fact]
    public void WithoutAnOverrideTheTileDye()
        => Assert.Equal(Black, new TileDyeOverrides().For(1, Black));

    [Fact]
    public void AnOverrideIsTheTilesOwn()
    {
        var overrides = new TileDyeOverrides();
        overrides.Set(1, Red, Black);

        Assert.Equal(Red, overrides.For(1, Black));
        Assert.Equal(Black, overrides.For(2, Black));
        Assert.True(overrides.Has(1));
    }

    [Fact]
    public void TheTileDyeAgainIsNoOverride()
    {
        var overrides = new TileDyeOverrides();
        overrides.Set(1, Red, Black);
        overrides.Set(1, Black, Black);

        Assert.False(overrides.Has(1));
    }

    [Fact]
    public void ClearDropsThemAll()
    {
        var overrides = new TileDyeOverrides();
        overrides.Set(1, Red, Black);
        overrides.Clear();

        Assert.Equal(Black, overrides.For(1, Black));
    }
}
