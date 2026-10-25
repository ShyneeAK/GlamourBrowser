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

    /// <summary>A click on a slot of the slot bar: <see cref="Toggle"/>, and the set mode ends.</summary>
    public static FilterState SelectSlot(FilterState filter, EquipSlot slot, bool additive)
        => filter with { Slots = Toggle(filter.Slots, slot, additive), Sets = false };

    /// <summary>A click on "Serien": switches the set mode; switching it on clears the slots, which do not apply to sets.</summary>
    public static FilterState ToggleSets(FilterState filter)
        => filter.Sets ? filter with { Sets = false } : filter with { Sets = true, Slots = new HashSet<EquipSlot>() };
}
