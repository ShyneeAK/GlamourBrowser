using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class SameModelTests
{
    private static GlamourItem Item(uint id, EquipSlot slot, ulong main, ulong sub = 0, int level = 50, string? name = null)
        => new(id, name ?? $"Item {id}", 0, slot, new HashSet<uint>(), level, 0, RaceGenderRestriction.None, ModelMain: main, ModelSub: sub);

    [Fact]
    public void ItemsWithTheSameModelInTheSameSlotShareALook()
    {
        var groups = SameModel.Groups([Item(1, EquipSlot.Body, 100, level: 90), Item(2, EquipSlot.Body, 100, level: 50), Item(3, EquipSlot.Body, 200)]);

        Assert.Equal([2u, 1u], groups[1].Select(i => i.Id));
        Assert.Same(groups[1], groups[2]);
        Assert.False(groups.ContainsKey(3));
    }

    [Fact]
    public void AnotherSlotOrSecondModelIsAnotherLook()
    {
        var groups = SameModel.Groups([
            Item(1, EquipSlot.Body, 100), Item(2, EquipSlot.Legs, 100),
            Item(3, EquipSlot.MainHand, 300, sub: 1), Item(4, EquipSlot.MainHand, 300, sub: 2)]);

        Assert.Empty(groups);
    }

    [Fact]
    public void ItemsWithoutAModelShareNothing()
        => Assert.Empty(SameModel.Groups([Item(1, EquipSlot.Body, 0), Item(2, EquipSlot.Body, 0)]));
}
