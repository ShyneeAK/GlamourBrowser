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
}
