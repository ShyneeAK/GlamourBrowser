using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Ui;

public static class SlotSelection
{
    /// <summary>
    /// Plain click selects only <paramref name="slot"/> (or clears when it was the only one);
    /// additive (Ctrl) click adds or removes it.
    /// </summary>
    public static IReadOnlySet<EquipSlot> Toggle(IReadOnlySet<EquipSlot> current, EquipSlot slot, bool additive)
    {
        if (!additive)
            return current.Count == 1 && current.Contains(slot) ? new HashSet<EquipSlot>() : new HashSet<EquipSlot> { slot };

        var result = new HashSet<EquipSlot>(current);
        if (!result.Remove(slot))
            result.Add(slot);
        return result;
    }

    /// <summary>A click on a slot of the slot bar: <see cref="Toggle"/>, and the set and wishlist modes end.</summary>
    public static FilterState SelectSlot(FilterState filter, EquipSlot slot, bool additive)
        => filter with { Slots = Toggle(filter.Slots, slot, additive), Sets = false, Wishlist = false };

    /// <summary>A click on "Ausrüstungs-Sets": switches the set mode; switching it on clears the slots, which do not apply to sets, and ends the wishlist mode.</summary>
    public static FilterState ToggleSets(FilterState filter)
        => filter.Sets ? filter with { Sets = false } : filter with { Sets = true, Slots = new HashSet<EquipSlot>(), Wishlist = false };

    /// <summary>A click on "Wunschliste": switches the wishlist mode; switching it on ends the set mode and clears the slots.</summary>
    public static FilterState ToggleWishlist(FilterState filter)
        => filter.Wishlist ? filter with { Wishlist = false } : filter with { Wishlist = true, Sets = false, Slots = new HashSet<EquipSlot>() };
}
