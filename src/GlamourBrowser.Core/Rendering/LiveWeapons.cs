using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

public static class LiveWeapons
{
    /// <summary>
    /// The weapon state the live preview applies: the draw and visibility toggles of the item card for items; a set
    /// always shows its weapon sheathed and visible, like its tile (the set card has no weapon toggles).
    /// </summary>
    public static (bool Drawn, bool Visible) Effective(TileSubject subject, bool drawn, bool visible)
        => subject is SetSubject ? (false, true) : (drawn, visible);
}
