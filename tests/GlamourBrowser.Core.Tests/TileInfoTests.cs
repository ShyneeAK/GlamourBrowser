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
}
