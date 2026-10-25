using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class ItemFilterTests
{
    private const byte Miqote = 4;
    private const byte Female = 1;

    private static GlamourItem Item(uint id, string name, EquipSlot slot, int level = 50, int dyes = 0, uint[]? jobs = null, RaceGenderRestriction? r = null)
        => new(id, name, 0, slot, new HashSet<uint>(jobs ?? []), level, dyes, r ?? RaceGenderRestriction.None);

    // Gladiator 1 is the base class of Paladin 19 (ClassJobParent); every other id is its own parent.
    private static uint ParentOf(uint job) => job == 19 ? 1u : job;

    private static IReadOnlyList<GlamourItem> Apply(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool>? isOwned = null)
        => ItemFilter.Apply(items, filter, isOwned ?? (_ => false), Miqote, Female, parentOf: ParentOf);

    private static uint[] Ids(IEnumerable<GlamourItem> items) => items.Select(i => i.Id).ToArray();

    [Fact]
    public void Dye_DoesNotFilter()
    {
        var items = new[] { Item(1, "Robe", EquipSlot.Body, dyes: 2), Item(2, "Wams", EquipSlot.Body) };
        Assert.Equal(Ids(Apply(items, new FilterState())), Ids(Apply(items, new FilterState { Dye = 81 })));
    }

    [Fact]
    public void SearchText_IsCaseInsensitiveSubstring()
    {
        var items = new[] { Item(1, "Äther-Filzrobe", EquipSlot.Body), Item(2, "Verschlissenes Wams", EquipSlot.Body) };
        Assert.Equal([1u], Ids(Apply(items, new FilterState { SearchText = "ROBE" })));
    }

    [Fact]
    public void Slots_EmptyMeansAll_OtherwiseOnlySelected()
    {
        var items = new[] { Item(1, "A", EquipSlot.Head), Item(2, "B", EquipSlot.Body), Item(3, "C", EquipSlot.Feet) };
        Assert.Equal(3, Apply(items, new FilterState()).Count);
        Assert.Equal([1u, 3u], Ids(Apply(items, new FilterState { Slots = new HashSet<EquipSlot> { EquipSlot.Head, EquipSlot.Feet } })).Order().ToArray());
    }

    [Fact]
    public void ClassJob_OnlyGearMadeForThatJob()
    {
        // Every job can wear everything as glamour: the filter finds gear for the job alone, its base class may be listed.
        var items = new[]
        {
            Item(1, "Tank", EquipSlot.Body, jobs: [19, 21]), Item(2, "GLA PLD", EquipSlot.Body, jobs: [1, 19]), Item(3, "PLD", EquipSlot.Body, jobs: [19]),
            Item(4, "WHM", EquipSlot.Body, jobs: [24]), Item(5, "GLA", EquipSlot.Body, jobs: [1]), Item(6, "Alle", EquipSlot.Body, jobs: [1, 19, 21, 24]),
        };
        Assert.Equal([2u, 3u], Ids(Apply(items, new FilterState { Jobs = new HashSet<uint> { 19 } })).Order().ToArray());
        Assert.Equal([4u], Ids(Apply(items, new FilterState { Jobs = new HashSet<uint> { 24 } })));
    }

    [Fact]
    public void Jobs_GearOnlyTheSelectedJobsWear()
    {
        // All crafters chosen (Carpenter 8, Blacksmith 9): gear for one or several of them, not gear a gatherer (16) wears too.
        var items = new[]
        {
            Item(1, "CRP", EquipSlot.MainHand, jobs: [8]), Item(2, "BSM", EquipSlot.MainHand, jobs: [9]), Item(3, "Handwerker", EquipSlot.Body, jobs: [8, 9]),
            Item(4, "Handwerker und Sammler", EquipSlot.Body, jobs: [8, 9, 16]), Item(5, "MIN", EquipSlot.MainHand, jobs: [16]),
        };
        Assert.Equal([1u, 2u, 3u], Ids(Apply(items, new FilterState { Jobs = new HashSet<uint> { 8, 9 } })).Order().ToArray());
    }

    [Fact]
    public void LevelRange_IsInclusive()
    {
        var items = new[] { Item(1, "A", EquipSlot.Body, 49), Item(2, "B", EquipSlot.Body, 50), Item(3, "C", EquipSlot.Body, 60), Item(4, "D", EquipSlot.Body, 61) };
        Assert.Equal([2u, 3u], Ids(Apply(items, new FilterState { MinLevel = 50, MaxLevel = 60 })).Order().ToArray());
    }

    [Fact]
    public void DyeableOnly_KeepsItemsWithAnyChannel()
    {
        var items = new[] { Item(1, "A", EquipSlot.Body, dyes: 0), Item(2, "B", EquipSlot.Body, dyes: 1), Item(3, "C", EquipSlot.Body, dyes: 2) };
        Assert.Equal([2u, 3u], Ids(Apply(items, new FilterState { DyeableOnly = true })).Order().ToArray());
        Assert.Equal(3, Apply(items, new FilterState()).Count);
    }

    [Fact]
    public void Ownership_OwnedAndMissing()
    {
        var items = new[] { Item(1, "A", EquipSlot.Body), Item(2, "B", EquipSlot.Body) };
        Assert.Equal([2u], Ids(Apply(items, new FilterState { Ownership = OwnershipFilter.Owned }, id => id == 2)));
        Assert.Equal([1u], Ids(Apply(items, new FilterState { Ownership = OwnershipFilter.Missing }, id => id == 2)));
    }

    [Fact]
    public void WearableOnly_HidesRestrictedItems()
    {
        var vieraFemale = new RaceGenderRestriction(new HashSet<byte> { 8 }, false, true);
        var items = new[] { Item(1, "A", EquipSlot.Body, r: vieraFemale), Item(2, "B", EquipSlot.Body) };
        Assert.Equal([2u], Ids(Apply(items, new FilterState { WearableOnly = true })));
        Assert.Equal(2, Apply(items, new FilterState { WearableOnly = false }).Count);
    }

    [Fact]
    public void Sort_LevelDescThenName()
    {
        var items = new[] { Item(1, "beta", EquipSlot.Body, 50), Item(2, "Alpha", EquipSlot.Body, 50), Item(3, "Zeta", EquipSlot.Body, 90) };
        Assert.Equal([3u, 2u, 1u], Ids(Apply(items, new FilterState())));
    }

    [Fact]
    public void PageCount_IsAtLeastOne()
    {
        Assert.Equal(1, ItemFilter.PageCount(0, 18));
        Assert.Equal(3, ItemFilter.PageCount(37, 18));
    }

    [Fact]
    public void Page_LastPageIsPartial()
    {
        var items = Enumerable.Range(1, 37).Select(i => Item((uint)i, $"I{i:D2}", EquipSlot.Body)).ToList();
        var page = ItemFilter.Page(items, 2, 18);
        Assert.Single(page);
        Assert.Equal(37u, page[0].Id);
    }

    [Fact]
    public void PageCount_InvalidPageSize_DoesNotThrow()
    {
        Assert.Equal(20, ItemFilter.PageCount(20, 0));
        Assert.Equal(3, ItemFilter.ClampPage(3, 20, -1));
    }

    [Fact]
    public void ClampPage_AfterFilterShrinks()
    {
        Assert.Equal(1, ItemFilter.ClampPage(5, 20, 18));
        Assert.Equal(0, ItemFilter.ClampPage(-1, 20, 18));
        Assert.Equal(0, ItemFilter.ClampPage(3, 0, 18));
    }
}
