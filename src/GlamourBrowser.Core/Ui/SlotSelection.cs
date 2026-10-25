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

    /// <summary>A click on a slot of the slot bar: <see cref="Toggle"/>, back to the items.</summary>
    public static FilterState SelectSlot(FilterState filter, EquipSlot slot, bool additive)
        => filter with { Slots = Toggle(filter.Slots, slot, additive), Mode = BrowseMode.Items };

    /// <summary>
    /// A click on a mode of the slot bar (sets, custom sets, wishlist): switches to it and clears the slots, which only
    /// apply to items; a click on the active mode goes back to the items.
    /// </summary>
    public static FilterState ToggleMode(FilterState filter, BrowseMode mode)
        => filter.Mode == mode ? filter with { Mode = BrowseMode.Items } : filter with { Mode = mode, Slots = new HashSet<EquipSlot>() };
}
