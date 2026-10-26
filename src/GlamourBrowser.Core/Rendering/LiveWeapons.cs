using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

public static class LiveWeapons
{
    /// <summary>
    /// The weapon state the live preview applies: the live view's draw and visibility toggles for items and when nothing
    /// is selected (a slot change drops the selection, the pose stays); a set always shows the weapon sheathed and visible
    /// (no weapon toggles there).
    /// </summary>
    public static (bool Drawn, bool Visible) Effective(TileSubject? subject, bool drawn, bool visible)
        => subject is SetSubject ? (false, true) : (drawn, visible);
}
