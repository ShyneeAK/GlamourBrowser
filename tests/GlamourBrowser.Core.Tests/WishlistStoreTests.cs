using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Wishlist;

namespace GlamourBrowser.Core.Tests;

public class WishlistStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 14, 0, 0, TimeSpan.FromHours(2));
    private static readonly SetId Dresser = new(SetKind.Dresser, 500);

    private static GlamourItem Item(uint id, EquipSlot slot) => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), 70, 0, RaceGenderRestriction.None);

    [Fact]
    public void Add_KeepsOrder_AndRejectsDuplicates()
    {
        var store = new WishlistStore(1);
        Assert.True(store.Add(WishKey.ForItem(7), Now));
        Assert.True(store.Add(WishKey.ForSet(Dresser), Now.AddMinutes(1)));
        Assert.True(store.Add(WishKey.ForItem(3), Now.AddMinutes(2)));
        Assert.False(store.Add(WishKey.ForItem(7), Now.AddMinutes(3)));

        Assert.Equal([WishKey.ForItem(7), WishKey.ForSet(Dresser), WishKey.ForItem(3)], store.Entries.Select(e => e.Key));
        Assert.Equal(Now, store.Entries[0].Added);
    }

    [Fact]
    public void Remove_RemovesOnlyThatEntry()
    {
        var store = new WishlistStore(1);
        store.Add(WishKey.ForItem(7), Now);
        store.Add(WishKey.ForSet(Dresser), Now);

        Assert.True(store.Remove(WishKey.ForItem(7)));
        Assert.False(store.Contains(WishKey.ForItem(7)));
        Assert.True(store.Contains(WishKey.ForSet(Dresser)));
        Assert.Single(store.Entries);
    }

    [Fact]
    public void Changed_OnlyOnRealChange()
    {
        var store = new WishlistStore(1);
        var raised = 0;
        store.Changed += () => raised++;

        store.Add(WishKey.ForItem(7), Now);
        store.Add(WishKey.ForItem(7), Now);
        store.Remove(WishKey.ForItem(8));
        Assert.Equal(1, raised);

        store.Remove(WishKey.ForItem(7));
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Variant_IsStoredAsItsBase()
    {
        var store = new WishlistStore(1, new Dictionary<uint, uint> { [12] = 10 });
        store.Add(WishKey.ForItem(12), Now);

        Assert.True(store.Contains(WishKey.ForItem(10)));
        Assert.True(store.Contains(WishKey.ForItem(12)));
        Assert.Equal(10u, store.Entries[0].Key.ItemId);
        Assert.True(store.Remove(WishKey.ForItem(12)));
        Assert.Empty(store.Entries);
    }

    [Fact]
    public void Variant_OfAListedBase_AddsNothing()
    {
        var store = new WishlistStore(1, new Dictionary<uint, uint> { [12] = 10 });
        store.Add(WishKey.ForItem(10), Now);

        Assert.False(store.Add(WishKey.ForItem(12), Now.AddMinutes(1)));
        Assert.Single(store.Entries);
    }

    [Fact]
    public void Variants_DoNotTouchSetKeys()
    {
        // A set id's value is not an item id, even when the number matches a variant.
        var store = new WishlistStore(1, new Dictionary<uint, uint> { [12] = 10 });
        store.Add(WishKey.ForSet(new SetId(SetKind.Model, 12)), Now);
        Assert.Equal(new SetId(SetKind.Model, 12), store.Entries[0].Key.Set);
    }

    [Fact]
    public void Of_MapsItemsAndSets()
    {
        var head = Item(1, EquipSlot.Head);
        var set = new SetSubject(new GlamourSet(Dresser, "A-Set", [new SetPiece(1, EquipSlot.Head), new SetPiece(2, EquipSlot.Body)], 70),
                                 [head, Item(2, EquipSlot.Body)]);

        Assert.Equal(new WishKey(WishKind.Item, 1, default), WishKey.Of(new ItemSubject(head)));
        Assert.Equal(new WishKey(WishKind.Set, 0, Dresser), WishKey.Of(set));
    }

    [Fact]
    public void Touch_PutsTheKeyAtTheEnd_AndDropsTheOldestBeyondTheLimit()
    {
        var store = new WishlistStore(1);
        var changes = 0;
        store.Changed += () => changes++;
        store.Touch(WishKey.ForItem(1), Now, 3);
        store.Touch(WishKey.ForItem(2), Now.AddMinutes(1), 3);
        store.Touch(WishKey.ForItem(3), Now.AddMinutes(2), 3);
        store.Touch(WishKey.ForItem(1), Now.AddMinutes(3), 3);
        // The newest again changes nothing.
        store.Touch(WishKey.ForItem(1), Now.AddMinutes(4), 3);
        store.Touch(WishKey.ForItem(4), Now.AddMinutes(5), 3);

        Assert.Equal([WishKey.ForItem(3), WishKey.ForItem(1), WishKey.ForItem(4)], store.Entries.Select(e => e.Key));
        Assert.Equal(Now.AddMinutes(3), store.Entries[1].Added);
        Assert.False(store.Contains(WishKey.ForItem(2)));
        Assert.Equal(5, changes);
    }
}
