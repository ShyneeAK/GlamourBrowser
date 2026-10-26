using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.UI;

internal static class SlotNames
{
    /// <summary>Every slot by its own name, the two rings apart (custom sets, glamour plates).</summary>
    public static string Full(EquipSlot slot) => Loc.T(slot switch
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
    });

    public static readonly (EquipSlot Slot, string Name)[] All =
    [
        (EquipSlot.MainHand, Loc.T("Waffe")),
        (EquipSlot.OffHand, Loc.T("Schild/Nebenhand")),
        (EquipSlot.Head, Loc.T("Kopf")),
        (EquipSlot.Body, Loc.T("Körper")),
        (EquipSlot.Hands, Loc.T("Hände")),
        (EquipSlot.Legs, Loc.T("Beine")),
        (EquipSlot.Feet, Loc.T("Füße")),
        (EquipSlot.Ears, Loc.T("Ohrringe")),
        (EquipSlot.Neck, Loc.T("Halskette")),
        (EquipSlot.Wrists, Loc.T("Armreife")),
        (EquipSlot.RingRight, Loc.T("Ringe")),
    ];
}
