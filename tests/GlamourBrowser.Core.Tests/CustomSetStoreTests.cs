using GlamourBrowser.Core.CustomSets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class CustomSetStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(1));
    private static readonly CustomPiece Robe = new(Body, 10, 1, 0);
    private static readonly CustomPiece Hat = new(Head, 20, 0, 0);

    [Fact]
    public void Add_GivesRisingIds_AndNewestFirst()
    {
        var store = new CustomSetStore(1);
        var first = store.Add("Erstes", [Robe], Now);
        var second = store.Add("Zweites", [Hat], Now.AddMinutes(1));

        Assert.Equal(1u, first.Id);
        Assert.Equal(2u, second.Id);
        Assert.Equal([2u, 1u], store.Sets.Select(s => s.Id));
        Assert.Equal(first, store.Find(1));
    }

    [Fact]
    public void Add_DoesNotReuseTheIdOfADeletedSet()
    {
        // A star on the deleted set must not land on the next new one.
        var store = new CustomSetStore(1);
        store.Add("Erstes", [Robe], Now);
        var second = store.Add("Zweites", [Hat], Now);
        store.Remove(second.Id);
        Assert.Equal(3u, store.Add("Drittes", [Hat], Now).Id);
    }

    [Fact]
    public void Update_ReplacesAndMovesToFront()
    {
        var store = new CustomSetStore(1);
        var first = store.Add("Erstes", [Robe], Now);
        store.Add("Zweites", [Hat], Now.AddMinutes(1));

        Assert.True(store.Update(first with { Name = "Umbenannt", Changed = Now.AddMinutes(2) }));
        Assert.Equal([1u, 2u], store.Sets.Select(s => s.Id));
        Assert.Equal("Umbenannt", store.Find(1)!.Name);
    }

    [Fact]
    public void Update_SameContent_ChangesNothing()
    {
        var store = new CustomSetStore(1);
        var set = store.Add("Erstes", [Robe], Now);
        // Another list with the same pieces and a later time is still the same set.
        Assert.False(store.Update(set with { Pieces = [new CustomPiece(Body, 10, 1, 0)], Changed = Now.AddHours(1) }));
        Assert.False(store.Update(set with { Id = 99 }));
        Assert.Equal(Now, store.Find(1)!.Changed);
    }

    [Fact]
    public void Remove_OnlyThatSet()
    {
        var store = new CustomSetStore(1);
        store.Add("Erstes", [Robe], Now);
        store.Add("Zweites", [Hat], Now);
        Assert.True(store.Remove(1));
        Assert.False(store.Remove(1));
        Assert.Equal([2u], store.Sets.Select(s => s.Id));
    }

    [Fact]
    public void Changed_OnlyOnRealChange()
    {
        var store = new CustomSetStore(1);
        var raised = 0;
        store.Changed += () => raised++;
        var set = store.Add("Erstes", [Robe], Now);
        store.Update(set);
        store.Remove(42);
        Assert.Equal(1, raised);
        store.Remove(set.Id);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Variants_BecomeTheirBase()
    {
        var store = new CustomSetStore(1, new Dictionary<uint, uint> { [12] = 10 });
        var set = store.Add("Varianten", [new CustomPiece(Body, 12, 3, 4)], Now);
        Assert.Equal(new CustomPiece(Body, 10, 3, 4), Assert.Single(set.Pieces));
    }

    [Theory]
    [InlineData("  Mein Set  ", "Mein Set")]
    [InlineData("", "Neues Set")]
    [InlineData("   ", "Neues Set")]
    [InlineData("1234567890123456789012345678901234567890123", "1234567890123456789012345678901234567890")]
    public void Name_TrimmedTo40_EmptyBecomesNeuesSet(string name, string expected)
        => Assert.Equal(expected, new CustomSetStore(1).Add(name, [Robe], Now).Name);

    [Fact]
    public void OnePiecePerSlot_LastWins_InSlotOrder_WithoutEmptyItems()
    {
        var set = new CustomSetStore(1).Add("Doppelt", [Robe, Hat, new CustomPiece(Body, 11, 0, 0), new CustomPiece(Feet, 0, 0, 0)], Now);
        Assert.Equal([Hat, new CustomPiece(Body, 11, 0, 0)], set.Pieces);
    }
}
