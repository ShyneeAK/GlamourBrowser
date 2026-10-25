using GlamourBrowser.Core.Items;

namespace GlamourBrowser.UI;

internal static class SlotNames
{
    /// <summary>Every slot by its own name, the two rings apart (custom sets, glamour plates).</summary>
    public static string Full(EquipSlot slot) => slot switch
    {
        EquipSlot.MainHand => "Waffe",
        EquipSlot.OffHand => "Nebenhand",
        EquipSlot.Head => "Kopf",
        EquipSlot.Body => "Körper",
        EquipSlot.Hands => "Hände",
        EquipSlot.Legs => "Beine",
        EquipSlot.Feet => "Füße",
        EquipSlot.Ears => "Ohrringe",
        EquipSlot.Neck => "Halskette",
        EquipSlot.Wrists => "Armreife",
        EquipSlot.RingRight => "Ring rechts",
        _ => "Ring links",
    };

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
