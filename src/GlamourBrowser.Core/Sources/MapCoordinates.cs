namespace GlamourBrowser.Core.Sources;

public static class MapCoordinates
{
    /// <summary>
    /// World X/Z to the map coordinate shown in game (same formula as Dalamud's MapUtil):
    /// 0.02·offset + 2048/scale + 0.02·value + 1, with Map.SizeFactor as scale (100 when missing).
    /// </summary>
    public static float FromWorld(float value, uint sizeFactor, int offset)
    {
        var scale = sizeFactor == 0 ? 100u : sizeFactor;
        return 0.02f * offset + 2048f / scale + 0.02f * value + 1f;
    }
}
