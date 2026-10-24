namespace GlamourBrowser.Rendering;

/// <summary>
/// One lock around the tile queue, the tile cache and the browser state, taken by the framework
/// tick and by the UI. Both normally run on the game's main thread, so this only guards the case
/// where they do not.
/// </summary>
public static class RenderSync
{
    public static readonly object Lock = new();
}
