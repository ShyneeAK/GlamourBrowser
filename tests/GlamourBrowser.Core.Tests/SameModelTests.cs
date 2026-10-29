using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class SameModelTests
{
    private static GlamourItem Item(uint id, EquipSlot slot, ulong main, ulong sub = 0, int level = 50, string? name = null)
        => new(id, name ?? $"Item {id}", 0, slot, new HashSet<uint>(), level, 0, RaceGenderRestriction.None, ModelMain: main, ModelSub: sub);

    [Fact]
    public void ItemsWithTheSameModelInTheSameSlotShareIt()
    {
        var groups = SameModel.Groups([Item(1, EquipSlot.Body, 100, level: 90), Item(2, EquipSlot.Body, 100, level: 50), Item(3, EquipSlot.Body, 200)]);

        Assert.Equal([2u, 1u], groups[1].Select(i => i.Id));
        Assert.Same(groups[1], groups[2]);
        Assert.False(groups.ContainsKey(3));
    }

    [Fact]
    public void FirstOfEach_KeepsTheFirstItemOfAModelInTheGivenOrder()
    {
        var items = new[]
        {
            Item(1, EquipSlot.Body, 0x301CA), Item(2, EquipSlot.Body, 200), Item(3, EquipSlot.Body, 0x101CA),
            Item(4, EquipSlot.Legs, 0x101CA), Item(5, EquipSlot.Body, 0), Item(6, EquipSlot.Body, 0),
        };

        // Another variant is the same model, another slot is not; items without a model all stay.
        Assert.Equal([1u, 2u, 4u, 5u, 6u], SameModel.FirstOfEach(items).Select(i => i.Id));
        // In another order (another sort) another item of the model is the first.
        Assert.Equal([3u, 4u, 5u, 6u, 2u], SameModel.FirstOfEach(items.Skip(2).Concat(items.Take(2))).Select(i => i.Id));
    }

    [Fact]
    public void Counts_CountAModelOncePerCategory_WhenOneOfItsItemsHasIt()
    {
        var items = new[] { Item(1, EquipSlot.Body, 100), Item(2, EquipSlot.Body, 100), Item(3, EquipSlot.Body, 200) };
        string[] CategoriesOf(uint id) => id switch { 1 => ["Shop"], 2 => ["Shop", "Raid"], _ => ["Shop"] };

        var counts = SameModel.Counts(items, CategoriesOf);

        Assert.Equal(2, counts["Shop"]);
        Assert.Equal(1, counts["Raid"]);
    }

    [Fact]
    public void AnotherVariantIsTheSameModel_TheSameVariantTogether()
    {
        // Mhigischer Rock des Zielens (0x101CA) and der Magie (0x301CA): model 0x1CA, variants 1 and 3.
        var groups = SameModel.Groups([Item(1, EquipSlot.Legs, 0x301CA, level: 10), Item(2, EquipSlot.Legs, 0x101CA, level: 70), Item(3, EquipSlot.Legs, 0x301CA, level: 5)]);

        Assert.Equal([2u, 3u, 1u], groups[1].Select(i => i.Id));
    }

    [Fact]
    public void WeaponsKeepTheirBaseAndDropOnlyTheVariant()
    {
        var groups = SameModel.Groups([
            Item(1, EquipSlot.MainHand, 0x0001_0005_0201), Item(2, EquipSlot.MainHand, 0x0002_0005_0201),
            Item(3, EquipSlot.MainHand, 0x0001_0006_0201)]);

        Assert.Equal([1u, 2u], groups[1].Select(i => i.Id));
        Assert.False(groups.ContainsKey(3));
    }

    [Fact]
    public void AnotherSlotOrSecondModelIsAnotherModel()
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
