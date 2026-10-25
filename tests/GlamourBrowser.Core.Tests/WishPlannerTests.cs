using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Wishlist;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class WishPlannerTests
{
    private static readonly ItemSource Sastasha = new(SourceCategory.Dungeon, "Sastasha", "", new DutyFinder(4));
    private static readonly ItemSource Vendor = new(SourceCategory.GilVendor, "Händlerin", "Limsa Lominsa", new MapFlag(128, 11, 9.5f, 11.2f), 1, 500);
    private static readonly ItemSource Quest = new(SourceCategory.Quest, "Ein Hauch von Abenteuer", "");

    private static GlamourItem Item(uint id, EquipSlot slot, string? name = null)
        => new(id, name ?? $"Item {id}", 0, slot, new HashSet<uint>(), 70, 0, RaceGenderRestriction.None);

    private static SetSubject Set(params GlamourItem[] pieces)
        => new(new GlamourSet(new SetId(SetKind.Model, pieces.Min(p => p.Id)), "A-Serie", pieces.Select(p => new SetPiece(p.Id, p.Slot)).ToList(), 70),
               pieces);

    private static WishPlan Plan(IReadOnlyList<TileSubject> entries, Func<uint, bool> isOwned, Dictionary<uint, ItemSource[]> sources)
        => WishPlanner.Plan(entries, isOwned, id => sources.GetValueOrDefault(id) ?? []);

    private static readonly Func<uint, bool> NothingOwned = _ => false;

    [Fact]
    public void IsDone_ItemOwned_SetComplete()
    {
        var set = Set(Item(1, Head), Item(2, Body));
        Assert.True(WishStatus.IsDone(new ItemSubject(Item(1, Head)), id => id == 1));
        Assert.False(WishStatus.IsDone(new ItemSubject(Item(1, Head)), NothingOwned));
        Assert.False(WishStatus.IsDone(set, id => id == 1));
        Assert.True(WishStatus.IsDone(set, id => id is 1 or 2));
    }

    [Fact]
    public void Groups_OnePerSource()
    {
        var body = Item(2, Body);
        var head = Item(1, Head);
        var plan = Plan([new ItemSubject(body), new ItemSubject(head), new ItemSubject(Item(3, Feet))], NothingOwned,
            new() { [1] = [Sastasha], [2] = [Sastasha], [3] = [Vendor] });

        Assert.Equal(2, plan.Groups.Count);
        Assert.Equal(Sastasha, plan.Groups[0].Source);
        Assert.Equal([1u, 2u], plan.Groups[0].Pieces.Select(p => p.Id));
        Assert.Equal(Vendor, plan.Groups[1].Source);
        Assert.Equal([3u], plan.Groups[1].Pieces.Select(p => p.Id));
    }

    [Fact]
    public void SameKey_KeepsTheFirstSourcesAction()
    {
        // Two items name the same vendor; the group takes action and cost of the first source.
        var other = Vendor with { Action = null, CostAmount = 900 };
        var plan = Plan([new ItemSubject(Item(1, Head)), new ItemSubject(Item(2, Body))], NothingOwned,
            new() { [1] = [Vendor], [2] = [other] });

        var group = Assert.Single(plan.Groups);
        Assert.Equal(Vendor, group.Source);
        Assert.Equal(2, group.Pieces.Count);
    }

    [Fact]
    public void PieceWithTwoSources_IsInBothGroups()
    {
        var plan = Plan([new ItemSubject(Item(1, Head))], NothingOwned, new() { [1] = [Sastasha, Vendor] });

        Assert.Equal([Sastasha, Vendor], plan.Groups.Select(g => g.Source));
        Assert.All(plan.Groups, g => Assert.Equal([1u], g.Pieces.Select(p => p.Id)));
        Assert.Equal(1, plan.Missing);
    }

    [Fact]
    public void Sets_CountOnlyMissingPieces()
    {
        var plan = Plan([Set(Item(1, Head), Item(2, Body), Item(3, Feet))], id => id == 2,
            new() { [1] = [Sastasha], [2] = [Sastasha], [3] = [Sastasha] });

        Assert.Equal([1u, 3u], Assert.Single(plan.Groups).Pieces.Select(p => p.Id));
        Assert.Equal(2, plan.Missing);
        Assert.Equal(3, plan.Total);
    }

    [Fact]
    public void SamePieceInTwoEntries_CountsOnce()
    {
        var head = Item(1, Head);
        var plan = Plan([new ItemSubject(head), Set(head, Item(2, Body))], NothingOwned,
            new() { [1] = [Sastasha], [2] = [Sastasha] });

        Assert.Equal(2, plan.Missing);
        Assert.Equal(2, plan.Total);
        Assert.Equal([1u, 2u], Assert.Single(plan.Groups).Pieces.Select(p => p.Id));
    }

    [Fact]
    public void SortedByCount_ThenRank_ThenTitle()
    {
        var omega = new ItemSource(SourceCategory.RaidSavage, "Omega", "");
        var beta = new ItemSource(SourceCategory.RaidSavage, "Beta", "");
        var plan = Plan([new ItemSubject(Item(1, Head)), new ItemSubject(Item(2, Body)), new ItemSubject(Item(3, Hands)),
                         new ItemSubject(Item(4, Legs)), new ItemSubject(Item(5, Feet))], NothingOwned,
            new() { [1] = [Quest], [2] = [omega], [3] = [beta], [4] = [Sastasha], [5] = [Sastasha] });

        Assert.Equal(["Sastasha", "Beta", "Omega", "Ein Hauch von Abenteuer"], plan.Groups.Select(g => g.Source!.Title));
    }

    [Fact]
    public void UnknownSource_IsLast()
    {
        // The unknown group is the biggest one and still comes last.
        var plan = Plan([new ItemSubject(Item(1, Head)), new ItemSubject(Item(2, Body)), new ItemSubject(Item(3, Feet))], NothingOwned,
            new() { [3] = [Vendor] });

        Assert.Equal([Vendor, null], plan.Groups.Select(g => g.Source));
        Assert.Equal([1u, 2u], plan.Groups[1].Pieces.Select(p => p.Id));
    }

    [Fact]
    public void MissingAndTotal()
    {
        // Done entries count in Total, but add nothing to Missing or the groups.
        var plan = Plan([new ItemSubject(Item(1, Head)), new ItemSubject(Item(2, Body)), Set(Item(3, Hands), Item(4, Legs))], id => id is 2 or 3,
            new() { [1] = [Vendor], [2] = [Vendor], [3] = [Vendor], [4] = [Vendor] });

        Assert.Equal(2, plan.Missing);
        Assert.Equal(4, plan.Total);
        Assert.Equal([1u, 4u], Assert.Single(plan.Groups).Pieces.Select(p => p.Id));
    }

    [Fact]
    public void NoEntries_GivesAnEmptyPlan()
    {
        var plan = Plan([], NothingOwned, new());
        Assert.Equal(0, plan.Missing);
        Assert.Equal(0, plan.Total);
        Assert.Empty(plan.Groups);
    }
}
