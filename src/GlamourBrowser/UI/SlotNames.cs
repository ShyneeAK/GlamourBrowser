using GlamourBrowser.Core.Items;

namespace GlamourBrowser.UI;

internal static class SlotNames
{
    public static readonly (EquipSlot Slot, string Name)[] All =
    [
        (EquipSlot.MainHand, "Waffe"),
        (EquipSlot.OffHand, "Schild/Nebenhand"),
        (EquipSlot.Head, "Kopf"),
        (EquipSlot.Body, "Körper"),
        (EquipSlot.Hands, "Hände"),
        (EquipSlot.Legs, "Beine"),
        (EquipSlot.Feet, "Füße"),
        (EquipSlot.Ears, "Ohrringe"),
        (EquipSlot.Neck, "Halskette"),
        (EquipSlot.Wrists, "Armreife"),
        (EquipSlot.RingRight, "Ringe"),
    ];
}
