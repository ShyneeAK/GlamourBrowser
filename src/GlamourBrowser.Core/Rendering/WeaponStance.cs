using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Which battle stance a drawn weapon is held in. The game picks the stance when the weapon is drawn and keeps it when
/// another weapon is loaded into the hand, so a weapon of another kind has to be sheathed first and drawn anew.
/// Weapons for the same jobs are of the same kind (all gunblades, all sword-and-shield swords).
/// </summary>
public static class WeaponStance
{
    /// <returns>The kind of a main-hand weapon (its jobs); null for anything else, which draws the own weapon.</returns>
    public static string? Of(TileSubject? subject)
        => subject is ItemSubject { Item: { Slot: EquipSlot.MainHand } item } ? string.Join(",", item.ClassJobIds.Order()) : null;
}
