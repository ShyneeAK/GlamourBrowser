using GlamourBrowser.Core.Items;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

public static class ItemSlots
{
    /// <summary>The first slot the item occupies (value 1); -1 marks slots it only blocks. Rings always count as the right ring.</summary>
    public static EquipSlot? SlotOf(EquipSlotCategory c)
    {
        if (c.MainHand == 1) return EquipSlot.MainHand;
        if (c.OffHand == 1) return EquipSlot.OffHand;
        if (c.Head == 1) return EquipSlot.Head;
        if (c.Body == 1) return EquipSlot.Body;
        if (c.Gloves == 1) return EquipSlot.Hands;
        if (c.Legs == 1) return EquipSlot.Legs;
        if (c.Feet == 1) return EquipSlot.Feet;
        if (c.Ears == 1) return EquipSlot.Ears;
        if (c.Neck == 1) return EquipSlot.Neck;
        if (c.Wrists == 1) return EquipSlot.Wrists;
        if (c.FingerL == 1 || c.FingerR == 1) return EquipSlot.RingRight;
        return null;
    }

    /// <summary>The slots the item blocks (-1) as a <see cref="SlotMask"/>: a robe over legs and feet, a two-handed weapon over the off hand.</summary>
    public static ushort BlocksOf(EquipSlotCategory c)
    {
        var blocked = new List<EquipSlot>();
        if (c.MainHand == -1) blocked.Add(EquipSlot.MainHand);
        if (c.OffHand == -1) blocked.Add(EquipSlot.OffHand);
        if (c.Head == -1) blocked.Add(EquipSlot.Head);
        if (c.Body == -1) blocked.Add(EquipSlot.Body);
        if (c.Gloves == -1) blocked.Add(EquipSlot.Hands);
        if (c.Legs == -1) blocked.Add(EquipSlot.Legs);
        if (c.Feet == -1) blocked.Add(EquipSlot.Feet);
        if (c.Ears == -1) blocked.Add(EquipSlot.Ears);
        if (c.Neck == -1) blocked.Add(EquipSlot.Neck);
        if (c.Wrists == -1) blocked.Add(EquipSlot.Wrists);
        return SlotMask.Of(blocked);
    }
}
