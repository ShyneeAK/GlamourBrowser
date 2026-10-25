using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Items;

public static class ItemFilter
{
    /// <param name="categoriesOf">Source categories per item; null while the source index is not ready (category filter ignored).</param>
    /// <param name="patchOf">Patch per item (0 = unknown); null ignores the expansion filter and sorts by level.</param>
    public static IReadOnlyList<GlamourItem> Apply(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
                                                   Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null)
        => Sort(Matching(items, filter, isOwned, race, sex, patchOf).Where(i => InCategories(i, filter, categoriesOf)), filter.Sort, patchOf);

    /// <summary>
    /// <see cref="Apply"/> plus the items that pass every filter except the source categories (the base of the
    /// category counts), from one pass over the items.
    /// </summary>
    public static (IReadOnlyList<GlamourItem> Results, IReadOnlyList<GlamourItem> WithoutCategories) ApplyWithCategoryBase(
        IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
        Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null)
    {
        var withoutCategories = Matching(items, filter, isOwned, race, sex, patchOf).ToList();
        return (Sort(withoutCategories.Where(i => InCategories(i, filter, categoriesOf)), filter.Sort, patchOf), withoutCategories);
    }

    private static bool InCategories(GlamourItem item, FilterState filter, Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf)
        => categoriesOf == null || filter.Categories.Count == 0 || categoriesOf(item.Id).Overlaps(filter.Categories);

    /// <summary>Every filter except the source categories, unsorted.</summary>
    private static IEnumerable<GlamourItem> Matching(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
                                                     Func<uint, float>? patchOf)
    {
        var search = filter.SearchText.Trim();
        return items
            .Where(i => search.Length == 0 || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(i => filter.Slots.Count == 0 || filter.Slots.Contains(i.Slot))
            .Where(i => filter.ClassJobId is not { } job || i.ClassJobIds.Contains(job))
            .Where(i => i.LevelEquip >= filter.MinLevel && i.LevelEquip <= filter.MaxLevel)
            .Where(i => filter.DyeCounts.Count == 0 || filter.DyeCounts.Contains(i.DyeCount))
            .Where(i => filter.Ownership switch
            {
                OwnershipFilter.Owned => isOwned(i.Id),
                OwnershipFilter.Missing => !isOwned(i.Id),
                _ => true,
            })
            .Where(i => !filter.WearableOnly || i.Restriction.Allows(race, sex))
            .Where(i => patchOf == null || filter.Expansions.Count == 0
                        || Expansions.FromPatch(patchOf(i.Id)) is { } expansion && filter.Expansions.Contains(expansion));
    }

    private static IReadOnlyList<GlamourItem> Sort(IEnumerable<GlamourItem> items, SortOrder sort, Func<uint, float>? patchOf)
    {
        if (sort == SortOrder.Newest && patchOf != null)
        {
            // Unknown patch (0) sorts last.
            return items
                .OrderByDescending(i => patchOf(i.Id) > 0)
                .ThenByDescending(i => patchOf(i.Id))
                .ThenByDescending(i => i.Id)
                .ToList();
        }

        return items
            .OrderByDescending(i => i.LevelEquip)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>At least one page; a page size below 1 counts as 1 (never divides by zero).</summary>
    public static int PageCount(int itemCount, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        return Math.Max(1, (itemCount + pageSize - 1) / pageSize);
    }

    public static int ClampPage(int pageIndex, int itemCount, int pageSize)
        => Math.Clamp(pageIndex, 0, PageCount(itemCount, pageSize) - 1);

    public static IReadOnlyList<GlamourItem> Page(IReadOnlyList<GlamourItem> items, int pageIndex, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        return items.Skip(pageIndex * pageSize).Take(pageSize).ToList();
    }
}
