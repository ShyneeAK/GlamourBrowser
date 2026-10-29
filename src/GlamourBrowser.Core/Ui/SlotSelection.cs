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

    /// <summary>
    /// Whether the slot bar's selection shows an item of <paramref name="itemSlot"/>: every slot without a selection;
    /// rings (all right rings in the catalog) under either ring slot.
    /// </summary>
    public static bool Shows(IReadOnlySet<EquipSlot> slots, EquipSlot itemSlot)
        => slots.Count == 0 || slots.Contains(itemSlot) || itemSlot == EquipSlot.RingRight && slots.Contains(EquipSlot.RingLeft);

    /// <summary>The hand the ring tiles show their ring on: the left one when only "Ring links" is chosen.</summary>
    public static EquipSlot RingHand(IReadOnlySet<EquipSlot> slots)
        => slots.Contains(EquipSlot.RingLeft) && !slots.Contains(EquipSlot.RingRight) ? EquipSlot.RingLeft : EquipSlot.RingRight;

    /// <summary>
    /// The hand a click on a ring's tile puts it on: the one its slot is chosen for; null (keep a worn ring where it is,
    /// else the first free hand) for other items and while every slot is shown.
    /// </summary>
    public static EquipSlot? ClickedRingHand(GlamourItem item, IReadOnlySet<EquipSlot> slots)
        => item.Slot switch
        {
            EquipSlot.RingLeft => EquipSlot.RingLeft,
            EquipSlot.RingRight when slots.Contains(EquipSlot.RingRight) => EquipSlot.RingRight,
            _ => null,
        };

    /// <summary>A click on a slot of the slot bar: <see cref="Toggle"/>, back to the items.</summary>
    public static FilterState SelectSlot(FilterState filter, EquipSlot slot, bool additive)
        => filter with { Slots = Toggle(filter.Slots, slot, additive), Mode = BrowseMode.Items };

    /// <summary>
    /// A click on a mode of the slot bar (sets, custom sets, wishlist): switches to it and clears the slots, which only
    /// apply to items; a click on the active mode goes back to the items.
    /// </summary>
    public static FilterState ToggleMode(FilterState filter, BrowseMode mode)
        => filter.Mode == mode ? filter with { Mode = BrowseMode.Items } : filter with { Mode = mode, Slots = new HashSet<EquipSlot>() };

    /// <summary>
    /// Whether a selection stays when the slot bar changes the view: an item while its slot is shown (all slots, or one of
    /// them chosen) or in the lists (wishlist, history, favourites), a set in the set modes and the wishlist. Otherwise it would stay selected for
    /// another slot, and a camera set for it would overwrite that slot's calibration unnoticed.
    /// </summary>
    public static bool KeepsSelection(TileSubject? selected, FilterState filter) => selected switch
    {
        null => true,
        ItemSubject { Item: var item } => filter.Mode is BrowseMode.Wishlist or BrowseMode.History or BrowseMode.Favorites
                                          || filter.Mode == BrowseMode.Items && (filter.Slots.Count == 0 || filter.Slots.Contains(item.Slot)
                                                                              && (item.Slot is not (EquipSlot.RingRight or EquipSlot.RingLeft) || item.Slot == RingHand(filter.Slots))),
        _ => filter.Mode is BrowseMode.Sets or BrowseMode.MySets or BrowseMode.Wishlist,
    };
}
