using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class SlotSelectionTests
{
    private static HashSet<EquipSlot> Set(params EquipSlot[] slots) => new(slots);

    [Fact]
    public void Toggle_PlainClickSelectsOnlyThat()
        => Assert.Equal(Set(EquipSlot.Body), SlotSelection.Toggle(Set(EquipSlot.Head, EquipSlot.Feet), EquipSlot.Body, additive: false));

    [Fact]
    public void Toggle_PlainClickOnSoleSelectionClears()
        => Assert.Empty(SlotSelection.Toggle(Set(EquipSlot.Head), EquipSlot.Head, additive: false));

    [Fact]
    public void Toggle_AdditiveAddsAndRemoves()
    {
        var both = SlotSelection.Toggle(Set(EquipSlot.Head), EquipSlot.Feet, additive: true);
        Assert.Equal(Set(EquipSlot.Head, EquipSlot.Feet), both);
        Assert.Equal(Set(EquipSlot.Feet), SlotSelection.Toggle(both, EquipSlot.Head, additive: true));
    }

    [Fact]
    public void ToggleMode_TurnsOnAndClearsSlots()
    {
        var on = SlotSelection.ToggleMode(new FilterState { Slots = Set(EquipSlot.Head), SearchText = "robe" }, BrowseMode.Sets);
        Assert.Equal(BrowseMode.Sets, on.Mode);
        Assert.Empty(on.Slots);
        Assert.Equal("robe", on.SearchText);
    }

    [Fact]
    public void ToggleMode_SameModeTurnsOff()
        => Assert.Equal(BrowseMode.Items, SlotSelection.ToggleMode(new FilterState { Mode = BrowseMode.MySets }, BrowseMode.MySets).Mode);

    [Fact]
    public void ToggleMode_SwitchesBetweenModes()
    {
        var sets = SlotSelection.ToggleMode(new FilterState(), BrowseMode.Sets);
        var mySets = SlotSelection.ToggleMode(sets, BrowseMode.MySets);
        Assert.Equal(BrowseMode.MySets, mySets.Mode);
        Assert.Equal(BrowseMode.Wishlist, SlotSelection.ToggleMode(mySets, BrowseMode.Wishlist).Mode);
    }

    [Theory]
    [InlineData(BrowseMode.Sets)]
    [InlineData(BrowseMode.MySets)]
    [InlineData(BrowseMode.Wishlist)]
    public void SelectSlot_EndsAnyMode(BrowseMode mode)
    {
        var state = SlotSelection.SelectSlot(new FilterState { Mode = mode }, EquipSlot.Head, additive: false);
        Assert.Equal(BrowseMode.Items, state.Mode);
        Assert.Equal(Set(EquipSlot.Head), state.Slots);
    }

    private static readonly ItemSubject Chest = new(new GlamourItem(1, "Brust", 0, EquipSlot.Body, new HashSet<uint>(), 50, 0, RaceGenderRestriction.None));

    [Fact]
    public void KeepsSelection_ItemOnlyWhileItsSlotIsShown()
    {
        Assert.True(SlotSelection.KeepsSelection(Chest, new FilterState()));
        Assert.True(SlotSelection.KeepsSelection(Chest, new FilterState { Slots = Set(EquipSlot.Body, EquipSlot.Feet) }));
        Assert.False(SlotSelection.KeepsSelection(Chest, new FilterState { Slots = Set(EquipSlot.Feet) }));
        Assert.False(SlotSelection.KeepsSelection(Chest, new FilterState { Mode = BrowseMode.Sets }));
        Assert.True(SlotSelection.KeepsSelection(Chest, new FilterState { Mode = BrowseMode.Wishlist }));
        Assert.True(SlotSelection.KeepsSelection(null, new FilterState { Slots = Set(EquipSlot.Feet) }));
    }

    [Fact]
    public void Shows_RingsUnderEitherRingSlot()
    {
        Assert.True(SlotSelection.Shows(Set(), EquipSlot.RingRight));
        Assert.True(SlotSelection.Shows(Set(EquipSlot.RingRight), EquipSlot.RingRight));
        Assert.True(SlotSelection.Shows(Set(EquipSlot.RingLeft), EquipSlot.RingRight));
        Assert.False(SlotSelection.Shows(Set(EquipSlot.Head), EquipSlot.RingRight));
    }

    [Fact]
    public void RingHand_LeftOnlyWithoutTheRightRing()
    {
        Assert.Equal(EquipSlot.RingLeft, SlotSelection.RingHand(Set(EquipSlot.RingLeft, EquipSlot.Head)));
        Assert.Equal(EquipSlot.RingRight, SlotSelection.RingHand(Set(EquipSlot.RingLeft, EquipSlot.RingRight)));
        Assert.Equal(EquipSlot.RingRight, SlotSelection.RingHand(Set()));
    }

    [Fact]
    public void ClickedRingHand_TheChosenRingSlot()
    {
        GlamourItem Ring(EquipSlot slot) => new(1, "Ring", 0, slot, new HashSet<uint>(), 1, 0, RaceGenderRestriction.None);
        Assert.Equal(EquipSlot.RingLeft, SlotSelection.ClickedRingHand(Ring(EquipSlot.RingLeft), Set(EquipSlot.RingLeft)));
        Assert.Equal(EquipSlot.RingRight, SlotSelection.ClickedRingHand(Ring(EquipSlot.RingRight), Set(EquipSlot.RingRight)));
        // All slots: a worn ring stays where it is.
        Assert.Null(SlotSelection.ClickedRingHand(Ring(EquipSlot.RingRight), Set()));
    }

    [Fact]
    public void KeepsSelection_ARingOnlyForItsHand()
    {
        var right = new ItemSubject(new GlamourItem(1, "Ring", 0, EquipSlot.RingRight, new HashSet<uint>(), 1, 0, RaceGenderRestriction.None));
        var left = new ItemSubject(right.Item with { Slot = EquipSlot.RingLeft });
        Assert.False(SlotSelection.KeepsSelection(right, new FilterState { Slots = Set(EquipSlot.RingLeft) }));
        Assert.True(SlotSelection.KeepsSelection(left, new FilterState { Slots = Set(EquipSlot.RingLeft) }));
        Assert.False(SlotSelection.KeepsSelection(left, new FilterState { Slots = Set(EquipSlot.RingRight) }));
    }
}
