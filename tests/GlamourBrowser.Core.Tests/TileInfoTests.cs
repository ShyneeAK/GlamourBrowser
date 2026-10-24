using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class TileInfoTests
{
    [Fact]
    public void Line_WithAndWithoutSource()
    {
        Assert.Equal("Lv. 90 · Raid (Episch)", TileInfo.Line(90, SourceCategory.RaidSavage));
        Assert.Equal("Lv. 90", TileInfo.Line(90, null));
    }

    [Fact]
    public void OwnedLine_ShowsWhereTheItemIs()
    {
        Assert.Equal("Lv. 90 · Kommode", TileInfo.OwnedLine(90, ["Kommode"]));
        Assert.Equal("Lv. 90 · Kommode +2", TileInfo.OwnedLine(90, ["Kommode", "Inventar", "Arsenal"]));
        Assert.Equal("Lv. 90", TileInfo.OwnedLine(90, []));
    }

    [Fact]
    public void FitIcons_TruncatesFromRight()
    {
        Assert.Equal(3, TileInfo.FitIcons(6, 16, 2, 60));
        Assert.Equal(0, TileInfo.FitIcons(6, 16, 2, 10));
        Assert.Equal(2, TileInfo.FitIcons(2, 16, 2, 200));
    }

    [Theory]
    [InlineData(200f, 16f, 3)]
    [InlineData(80f, 16f, 2)]
    [InlineData(40f, 16f, 1)]
    [InlineData(20f, 16f, 0)]
    public void InfoRows_DropIconsThenLineTwoOnShortTiles(float tileHeight, float lineHeight, int expected)
        => Assert.Equal(expected, TileInfo.InfoRows(tileHeight, lineHeight));

    [Fact]
    public void TraitTexts()
    {
        Assert.Equal("Färbbar (1 Farbkanal)", TileInfo.DyeText(1));
        Assert.Equal("Färbbar (2 Farbkanäle)", TileInfo.DyeText(2));
        Assert.Equal("Marktbrett", TileInfo.TradeText(GlamourBrowser.Core.Items.Tradability.Marketable));
        Assert.Equal("Handelbar", TileInfo.TradeText(GlamourBrowser.Core.Items.Tradability.Tradable));
        Assert.Equal("Gebunden", TileInfo.TradeText(GlamourBrowser.Core.Items.Tradability.Bound));
    }

    [Fact]
    public void FitRow_RightIconFirst_ThenLeftIcons()
    {
        Assert.Equal((3, true), TileInfo.FitRow(5, true, 16, 2, 80));  // right takes 18, left 3·16+2·2 = 52 of 62
        Assert.Equal((4, false), TileInfo.FitRow(4, false, 16, 2, 80));
        Assert.Equal((0, true), TileInfo.FitRow(3, true, 16, 2, 20));
        Assert.Equal((0, false), TileInfo.FitRow(3, true, 16, 2, 10));
    }
}
