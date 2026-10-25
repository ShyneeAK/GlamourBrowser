namespace GlamourBrowser.Core.Items;

/// <summary>A set of equipment slots as bits (bit = slot id), cheap to compare and to keep per view.</summary>
public static class SlotMask
{
    public static ushort Of(IEnumerable<EquipSlot> slots) => (ushort)slots.Aggregate(0, (mask, slot) => mask | 1 << (int)slot);

    /// <summary>The mask of a single slot, without a list (tile keys are made every frame).</summary>
    public static ushort Of(EquipSlot slot) => (ushort)(1 << (int)slot);

    public static bool Contains(ushort mask, EquipSlot slot) => (mask & 1 << (int)slot) != 0;
}
