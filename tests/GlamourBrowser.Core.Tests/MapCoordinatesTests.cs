using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class MapCoordinatesTests
{
    [Fact]
    public void Origin_OnDefaultScaleMap() => Assert.Equal(21.48f, MapCoordinates.FromWorld(0f, 100, 0), 0.001f);

    [Fact]
    public void OffsetAndScaleApply()
    {
        // 0.02·(−80) + 2048/200 + 0.02·100 + 1 = −1.6 + 10.24 + 2 + 1
        Assert.Equal(11.64f, MapCoordinates.FromWorld(100f, 200, -80), 0.001f);
    }

    [Fact]
    public void InvalidScaleIsTreatedAsDefault() => Assert.Equal(21.48f, MapCoordinates.FromWorld(0f, 0, 0), 0.001f);
}
