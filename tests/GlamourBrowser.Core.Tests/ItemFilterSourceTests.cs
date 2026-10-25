using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class ItemFilterSourceTests
{
    private static GlamourItem Item(uint id, int level = 50)
        => new(id, $"Item {id}", 0, EquipSlot.Body, new HashSet<uint>(), level, 0, RaceGenderRestriction.None);

    private static readonly Dictionary<uint, IReadOnlySet<SourceCategory>> Categories = new()
    {
        [1] = new HashSet<SourceCategory> { SourceCategory.RaidSavage, SourceCategory.GilVendor },
        [2] = new HashSet<SourceCategory> { SourceCategory.Crafting },
    };

    private static uint[] Apply(IEnumerable<GlamourItem> items, FilterState filter,
                                Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf, Func<uint, float>? patchOf)
        => ItemFilter.Apply(items, filter, _ => false, 1, 0, categoriesOf, patchOf).Select(i => i.Id).ToArray();

    [Fact]
    public void Categories_AnyOverlapMatches()
    {
        var filter = new FilterState { Categories = new HashSet<SourceCategory> { SourceCategory.RaidSavage } };
        Assert.Equal([1u], Apply([Item(1), Item(2)], filter, id => Categories[id], null));
    }

    [Fact]
    public void Categories_IgnoredWhenIndexNotReady()
    {
        var filter = new FilterState { Categories = new HashSet<SourceCategory> { SourceCategory.RaidSavage } };
        Assert.Equal(2, Apply([Item(1), Item(2)], filter, null, null).Length);
    }

    [Fact]
    public void Expansions_FilterByPatch()
    {
        var patches = new Dictionary<uint, float> { [1] = 6.4f, [2] = 7.0f, [3] = 0f };
        var filter = new FilterState { Expansions = new HashSet<Expansion> { Expansion.Endwalker } };
        Assert.Equal([1u], Apply([Item(1), Item(2), Item(3)], filter, null, id => patches[id]));
    }

    [Fact]
    public void SortNewest_PatchDescThenIdDesc_UnknownLast()
    {
        var patches = new Dictionary<uint, float> { [1] = 6.4f, [2] = 7.0f, [3] = 7.0f, [4] = 0f };
        var filter = new FilterState { Sort = SortOrder.Newest };
        Assert.Equal([3u, 2u, 1u, 4u], Apply([Item(1), Item(2), Item(3), Item(4)], filter, null, id => patches[id]));
    }

    [Fact]
    public void SortNewest_WithoutPatchData_FallsBackToLevel()
    {
        var filter = new FilterState { Sort = SortOrder.Newest };
        Assert.Equal([2u, 1u], Apply([Item(1, 50), Item(2, 90)], filter, null, null));
    }

    [Fact]
    public void WithCategoryBase_ResultsAsApply_BaseSkipsOnlyTheCategoryFilter()
    {
        var categories = new Dictionary<uint, IReadOnlySet<SourceCategory>>
        {
            [1] = new HashSet<SourceCategory> { SourceCategory.RaidSavage },
            [2] = new HashSet<SourceCategory> { SourceCategory.Crafting },
            [3] = new HashSet<SourceCategory> { SourceCategory.RaidSavage },
        };
        var items = new[] { Item(1, 50), Item(2, 60), Item(3, 10) };
        var filter = new FilterState { MinLevel = 20, Categories = new HashSet<SourceCategory> { SourceCategory.RaidSavage } };

        var (results, withoutCategories) = ItemFilter.ApplyWithCategoryBase(items, filter, _ => false, 1, 0, id => categories[id], null);

        Assert.Equal(Apply(items, filter, id => categories[id], null), results.Select(i => i.Id).ToArray());
        Assert.Equal([1u, 2u], withoutCategories.Select(i => i.Id).Order().ToArray());   // the level filter still applies
    }
}
