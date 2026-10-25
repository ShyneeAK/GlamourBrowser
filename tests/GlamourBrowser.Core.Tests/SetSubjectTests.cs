using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class SetSubjectTests
{
    private static GlamourItem Item(uint id, EquipSlot slot, uint[]? jobs = null, RaceGenderRestriction? restriction = null)
        => new(id, $"Item {id}", 0, slot, (jobs ?? [19, 21]).ToHashSet(), 70, 0, restriction ?? RaceGenderRestriction.None);

    private static GlamourSet Set(params (uint Id, EquipSlot Slot)[] pieces)
        => new(new SetId(SetKind.Model, pieces.Min(p => p.Id)), "A-Serie", pieces.Select(p => new SetPiece(p.Id, p.Slot)).ToList(), 70);

    private static SetSubject Subject(params GlamourItem[] items)
        => SetSubject.Resolve(Set(items.Select(i => (i.Id, i.Slot)).ToArray()), id => items.FirstOrDefault(i => i.Id == id))!;

    [Fact]
    public void SlotMask_OfAndContains()
    {
        var mask = SlotMask.Of([Head, RingRight]);
        Assert.True(SlotMask.Contains(mask, Head));
        Assert.True(SlotMask.Contains(mask, RingRight));
        Assert.False(SlotMask.Contains(mask, Body));
    }

    [Fact]
    public void Resolve_DropsUnknownPieces_AndNullBelowTwo()
    {
        var items = new[] { Item(1, Head), Item(2, Body) };
        var resolved = SetSubject.Resolve(Set((1, Head), (2, Body), (3, Feet)), id => items.FirstOrDefault(i => i.Id == id));
        Assert.Equal([1u, 2u], resolved!.Pieces.Select(p => p.Id));
        Assert.Null(SetSubject.Resolve(Set((1, Head), (3, Feet)), id => items.FirstOrDefault(i => i.Id == id)));
    }

    [Fact]
    public void Resolve_KeepsFirstPiecePerSlot()
    {
        // Both rings are RingRight in the catalog.
        var items = new[] { Item(1, Head), Item(2, RingRight), Item(3, RingRight) };
        var resolved = SetSubject.Resolve(Set((1, Head), (2, RingRight), (3, RingLeft)), id => items.FirstOrDefault(i => i.Id == id));
        Assert.Equal([1u, 2u], resolved!.Pieces.Select(p => p.Id));
    }

    [Fact]
    public void ArmourJobs_IsTheIntersection()
        => Assert.Equal(new HashSet<uint> { 21 }, Subject(Item(1, Head, [19, 21]), Item(2, Body, [21, 23]), Item(3, Ears, [1, 2])).ArmourJobs);

    [Fact]
    public void ArmourJobs_EmptyWhenAPieceHasNoJobs()
        => Assert.Empty(Subject(Item(1, Head, [19, 21]), Item(2, Body, [])).ArmourJobs);

    [Fact]
    public void ArmourJobs_UsesAllPieces_WithoutArmour()
        => Assert.Equal(new HashSet<uint> { 19 }, Subject(Item(1, Ears, [19, 21]), Item(2, Neck, [19])).ArmourJobs);

    [Fact]
    public void Allows_RequiresEveryPiece()
    {
        var femaleOnly = new RaceGenderRestriction(new HashSet<byte> { 1, 2, 3, 4, 5, 6, 7, 8 }, false, true);
        var set = Subject(Item(1, Head), Item(2, Body, restriction: femaleOnly));
        Assert.True(set.Allows(4, 1));
        Assert.False(set.Allows(4, 0));
    }

    [Fact]
    public void Progress_CountsOwnedPieces()
    {
        var set = Subject(Item(1, Head), Item(2, Body), Item(3, Hands), Item(4, Legs), Item(5, Feet));
        var progress = SetProgress.Of(set, id => id <= 3);
        Assert.Equal(new SetProgress(3, 5), progress);
        Assert.False(progress.Complete);
        Assert.True(SetProgress.Of(set, _ => true).Complete);
    }

    [Fact]
    public void Subjects_IdsDiffer_AndSetsUseTheBodyCamera()
    {
        var item = Item(1, Head);
        var set = Subject(item, Item(2, Body));
        Assert.NotEqual(new ItemSubject(item).Id, set.Id);
        Assert.Equal(Body, set.CameraSlot);
        Assert.Equal(Head, new ItemSubject(item).CameraSlot);
        Assert.Equal(SlotMask.Of([Head, Body]), set.Slots);
    }

    [Fact]
    public void LiveWeapons_SetsAlwaysShowTheWeaponSheathed()
        => Assert.Equal((false, true), LiveWeapons.Effective(Subject(Item(1, MainHand), Item(2, Body)), drawn: true, visible: false));

    [Fact]
    public void LiveWeapons_ItemsKeepTheToggles()
        => Assert.Equal((true, false), LiveWeapons.Effective(new ItemSubject(Item(1, MainHand)), drawn: true, visible: false));

    [Fact]
    public void ResolveAll_DropsSetsWithTheSamePiecesAsAnEarlierOne()
    {
        // Set 2 is the "+1" series: its pieces resolve to the base items of set 1.
        var items = new[] { Item(1, Head), Item(2, Body) };
        var baseOf = new Dictionary<uint, uint> { [11] = 1, [12] = 2 };
        var sets = new[] { Set((1, Head), (2, Body)), Set((11, Head), (12, Body)) };
        var resolved = SetSubject.ResolveAll(sets, id => items.FirstOrDefault(i => i.Id == baseOf.GetValueOrDefault(id, id)));
        Assert.Equal(new SetId(SetKind.Model, 1), Assert.Single(resolved).Set.Id);
    }
}
