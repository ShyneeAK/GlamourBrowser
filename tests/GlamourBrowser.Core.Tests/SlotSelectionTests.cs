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
    public void ToggleSets_TurnsOnAndClearsSlots()
    {
        var on = SlotSelection.ToggleSets(new FilterState { Slots = Set(EquipSlot.Head) });
        Assert.True(on.Sets);
        Assert.Empty(on.Slots);
        Assert.False(SlotSelection.ToggleSets(on).Sets);
    }

    [Fact]
    public void SelectSlot_LeavesSetMode()
    {
        var state = SlotSelection.SelectSlot(new FilterState { Sets = true }, EquipSlot.Head, additive: false);
        Assert.False(state.Sets);
        Assert.Equal(Set(EquipSlot.Head), state.Slots);
    }
}
