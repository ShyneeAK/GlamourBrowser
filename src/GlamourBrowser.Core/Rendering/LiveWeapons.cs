using GlamourBrowser.Core.CustomSets;
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

    /// <summary>
    /// Whether a tile shows the weapons: when the tile wears a weapon itself, or its look (the try-on around it) holds a
    /// main hand. Without one the game would show a default sword, so the weapons are hidden like in the live view.
    /// </summary>
    public static bool TileShows(TileSubject subject, IReadOnlyList<CustomPiece> look)
        => subject.Wear.Any(i => i.Slot is EquipSlot.MainHand or EquipSlot.OffHand)
           || look.Any(p => p.Slot == EquipSlot.MainHand && p.ItemId != 0);
}
