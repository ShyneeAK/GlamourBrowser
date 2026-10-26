using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

public static class LiveWeapons
{
    /// <summary>
    /// The weapon state the live preview applies: the live view's draw and visibility toggles for items and when nothing
    /// is selected (a slot change drops the selection, the pose stays); a set always shows the weapon sheathed and visible
    /// (no weapon toggles there). Without a main hand in the look (taken off) the weapon is hidden and sheathed: the game
    /// would show a default sword.
    /// </summary>
    public static (bool Drawn, bool Visible) Effective(TileSubject? subject, bool drawn, bool visible, bool hasMainHand = true)
        => !hasMainHand ? (false, false) : subject is SetSubject ? (false, true) : (drawn, visible);
}
