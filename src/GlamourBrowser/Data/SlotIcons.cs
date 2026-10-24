using System.Collections.Generic;
using GlamourBrowser.Core.Items;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Data;

/// <summary>Slot icons taken from the game's item categories (ItemUICategory.Icon).</summary>
public static class SlotIcons
{
    private static readonly Dictionary<EquipSlot, uint> CategoryRows = new()
    {
        [EquipSlot.MainHand] = 2,   // Gladiator's arm
        [EquipSlot.OffHand] = 11,   // Shield
        [EquipSlot.Head] = 34,
        [EquipSlot.Body] = 35,
        [EquipSlot.Hands] = 37,
        [EquipSlot.Legs] = 36,
        [EquipSlot.Feet] = 38,
        [EquipSlot.Ears] = 41,
        [EquipSlot.Neck] = 40,
        [EquipSlot.Wrists] = 42,
        [EquipSlot.RingRight] = 43,
        [EquipSlot.RingLeft] = 43,
    };

    private static readonly Dictionary<EquipSlot, uint> Cache = new();

    public static uint IconFor(EquipSlot slot)
    {
        if (Cache.TryGetValue(slot, out var icon))
            return icon;
        icon = CategoryRows.TryGetValue(slot, out var row) && Plugin.DataManager.GetExcelSheet<ItemUICategory>().TryGetRow(row, out var category)
            ? (uint)category.Icon
            : 0;
        return Cache[slot] = icon;
    }
}
