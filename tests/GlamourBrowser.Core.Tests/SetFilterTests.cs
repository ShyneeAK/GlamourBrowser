using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class SetFilterTests
{
    private static GlamourItem Item(uint id, string name, EquipSlot slot, uint[]? jobs = null, RaceGenderRestriction? r = null)
        => new(id, name, 0, slot, (jobs ?? [19, 21]).ToHashSet(), 70, 2, r ?? RaceGenderRestriction.None);

    private static SetSubject Set(uint id, string name, int level, params GlamourItem[] pieces)
        => new(new GlamourSet(new SetId(SetKind.Model, id), name, pieces.Select(p => new SetPiece(p.Id, p.Slot)).ToList(), level), pieces);

    private static readonly RaceGenderRestriction FemaleOnly = new(new HashSet<byte> { 1, 2, 3, 4, 5, 6, 7, 8 }, false, true);

    // A: level 60, scouting jobs. B: level 90, armour wearable only by job 19 in common, body female only.
    private static readonly SetSubject A = Set(1, "Asuran-Serie des Spähens", 60, Item(11, "Asuran-Hachigane", Head, [30, 39]), Item(12, "Asuran-Togi", Body, [30, 39]));
    private static readonly SetSubject B = Set(2, "Garlond-Serie", 90, Item(21, "Garlond-Helm", Head, [19, 21]), Item(22, "Garlond-Körperpanzer", Body, [19], FemaleOnly),
                                               Item(23, "Garlond-Ohrringe", Ears, [19, 21, 30]));

    private static uint[] Ids(IEnumerable<SetSubject> sets) => sets.Select(s => s.Set.Id.Value).ToArray();

    private static (IReadOnlyList<SetSubject> Results, IReadOnlyList<SetSubject> WithoutCategories) Apply(
        FilterState filter, Func<uint, bool>? owned = null, Func<uint, IReadOnlySet<SourceCategory>>? categories = null,
        Func<uint, float>? patch = null, byte sex = 1, IEnumerable<SetSubject>? sets = null)
        => SetFilter.Apply(sets ?? [A, B], filter with { Sets = true }, owned ?? (_ => false), 4, sex, categories, patch, job => job == 19 ? 1u : job);

    [Fact]
    public void Search_MatchesSetOrPieceName()
    {
        Assert.Equal([1u], Ids(Apply(new FilterState { SearchText = "togi" }).Results));
        Assert.Equal([2u], Ids(Apply(new FilterState { SearchText = "garlond-serie" }).Results));
    }

    [Fact]
    public void Job_EveryArmourPieceMadeForThatJob()
    {
        // B's helmet fits two jobs: not job gear. C is: the accessory does not count while there is armour.
        var c = Set(3, "Paladin-Set", 90, Item(31, "Paladin-Helm", Head, [19]), Item(32, "Paladin-Panzer", Body, [1, 19]),
                    Item(33, "Ohrringe", Ears, [19, 21, 30]));
        Assert.Equal([3u], Ids(Apply(new FilterState { Jobs = new HashSet<uint> { 19 } }, sets: [A, B, c]).Results));
        Assert.Empty(Apply(new FilterState { Jobs = new HashSet<uint> { 21 } }, sets: [A, B, c]).Results);
        // Both tank jobs of B's helmet chosen: now all of B's armour is gear for them.
        Assert.Equal([2u, 3u], Ids(Apply(new FilterState { Jobs = new HashSet<uint> { 19, 21 } }, sets: [A, B, c]).Results).Order().ToArray());
    }

    [Fact]
    public void Level_UsesSetLevel() => Assert.Equal([2u], Ids(Apply(new FilterState { MinLevel = 70 }).Results));

    [Fact]
    public void Owned_MeansComplete()
        => Assert.Equal([1u], Ids(Apply(new FilterState { Ownership = OwnershipFilter.Owned }, id => id is 11 or 12 or 21).Results));

    [Fact]
    public void Missing_MeansAnyPieceMissing()
        => Assert.Equal([2u], Ids(Apply(new FilterState { Ownership = OwnershipFilter.Missing }, id => id is 11 or 12 or 21).Results));

    [Fact]
    public void WearableOnly_RequiresEveryPiece() => Assert.Equal([1u], Ids(Apply(new FilterState(), sex: 0).Results));

    [Fact]
    public void Category_AnyPiece_AndCountsCountSets()
    {
        IReadOnlySet<SourceCategory> Categories(uint id) => id == 11
            ? new HashSet<SourceCategory> { SourceCategory.Dungeon }
            : new HashSet<SourceCategory> { SourceCategory.Other };
        var (results, withoutCategories) = Apply(new FilterState { Categories = new HashSet<SourceCategory> { SourceCategory.Dungeon } }, categories: Categories);
        Assert.Equal([1u], Ids(results));
        Assert.Equal(2, withoutCategories.Count);
        var counts = SetFilter.Counts(withoutCategories, Categories);
        Assert.Equal(1, counts[SourceCategory.Dungeon]);
        Assert.Equal(2, counts[SourceCategory.Other]);
    }

    [Fact]
    public void Expansion_UsesNewestPiece()
    {
        float Patch(uint id) => id switch { 11 => 6.0f, 12 => 7.0f, _ => 5.0f };
        Assert.Equal([1u], Ids(Apply(new FilterState { Expansions = new HashSet<Expansion> { Expansion.Dawntrail } }, patch: Patch).Results));
    }

    [Fact]
    public void Newest_SortsByNewestPiece()
    {
        float Patch(uint id) => id switch { 11 => 6.0f, 12 => 7.0f, _ => 5.0f };
        Assert.Equal([1u, 2u], Ids(Apply(new FilterState { Sort = SortOrder.Newest }, patch: Patch).Results));
    }

    [Fact]
    public void Level_SortsByLevelThenName()
    {
        var c = Set(3, "Alpha-Serie", 90, Item(31, "Alpha-Hut", Head), Item(32, "Alpha-Robe", Body));
        Assert.Equal([3u, 2u, 1u], Ids(Apply(new FilterState(), sets: [A, B, c]).Results));
    }

    [Fact]
    public void DyeAndSlots_AreIgnored()
        => Assert.Equal(2, Apply(new FilterState { DyeableOnly = true, Slots = new HashSet<EquipSlot> { Feet } }).Results.Count);

    [Fact]
    public void EmptyInput_GivesEmptyResults()
    {
        var (results, withoutCategories) = Apply(new FilterState(), sets: []);
        Assert.Empty(results);
        Assert.Empty(withoutCategories);
    }
}
