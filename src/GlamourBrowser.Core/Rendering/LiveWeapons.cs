using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

public static class LiveWeapons
{
    /// <summary>
    /// The weapon state the live preview applies: the draw and visibility toggles of the item card for items; a set, and
    /// the own outfit when nothing is selected, always show the weapon sheathed and visible (no weapon toggles there).
    /// </summary>
    public static (bool Drawn, bool Visible) Effective(TileSubject? subject, bool drawn, bool visible)
        => subject is ItemSubject ? (drawn, visible) : (false, true);
}
