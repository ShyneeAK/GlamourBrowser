using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Items;

public static class ItemFilter
{
    /// <param name="categoriesOf">Source categories per item; null while the source index is not ready (category filter ignored).</param>
    /// <param name="patchOf">Patch per item (0 = unknown); null ignores the expansion filter and sorts by level.</param>
    /// <param name="parentOf">A job's base class (ClassJob.ClassJobParent) for the job filter; null treats every id as its own.</param>
    /// <param name="wristOf">The arm a bracelet is on; null while not every bracelet is read (side filter ignored).</param>
    public static IReadOnlyList<GlamourItem> Apply(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
                                                   Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null,
                                                   Func<uint, uint>? parentOf = null, Func<uint, Rendering.WristSide>? wristOf = null)
        => Sort(Matching(items, filter, isOwned, race, sex, patchOf, parentOf, wristOf).Where(i => InCategories(i, filter, categoriesOf)), filter.Sort, patchOf);

    /// <summary>
    /// <see cref="Apply"/> plus the items that pass every filter except the source categories (the base of the
    /// category counts), from one pass over the items.
    /// </summary>
    public static (IReadOnlyList<GlamourItem> Results, IReadOnlyList<GlamourItem> WithoutCategories) ApplyWithCategoryBase(
        IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
        Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null, Func<uint, uint>? parentOf = null,
        Func<uint, Rendering.WristSide>? wristOf = null)
    {
        var withoutCategories = Matching(items, filter, isOwned, race, sex, patchOf, parentOf, wristOf).ToList();
        return (Sort(withoutCategories.Where(i => InCategories(i, filter, categoriesOf)), filter.Sort, patchOf), withoutCategories);
    }

    private static bool InCategories(GlamourItem item, FilterState filter, Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf)
        => categoriesOf == null || filter.Categories.Count == 0 || categoriesOf(item.Id).Overlaps(filter.Categories);

    /// <summary>Every filter except the source categories, unsorted.</summary>
    private static IEnumerable<GlamourItem> Matching(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
                                                     Func<uint, float>? patchOf, Func<uint, uint>? parentOf, Func<uint, Rendering.WristSide>? wristOf)
    {
        var search = filter.SearchText.Trim();
        var jobs = new JobFilter(filter.Jobs, parentOf ?? (job => job), filter.JobsExclusive);
        return items
            .Where(i => search.Length == 0 || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(i => filter.Slots.Count == 0 || filter.Slots.Contains(i.Slot))
            .Where(i => filter.Jobs.Count == 0 || jobs.Fits(i.ClassJobIds))
            .Where(i => i.LevelEquip >= filter.MinLevel && i.LevelEquip <= filter.MaxLevel)
            .Where(i => !filter.DyeableOnly || i.DyeCount > 0)
            .Where(i => filter.Wrists.Count == 0 || wristOf == null || i.Slot != EquipSlot.Wrists || filter.Wrists.Contains(wristOf(i.Id)))
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
        => Sorting.Apply(items, sort, i => i.LevelEquip, i => i.Name, patchOf == null ? null : i => patchOf(i.Id), i => i.Id);

    /// <summary>At least one page; a page size below 1 counts as 1 (never divides by zero).</summary>
    public static int PageCount(int itemCount, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        return Math.Max(1, (itemCount + pageSize - 1) / pageSize);
    }

    public static int ClampPage(int pageIndex, int itemCount, int pageSize)
        => Math.Clamp(pageIndex, 0, PageCount(itemCount, pageSize) - 1);

    public static IReadOnlyList<T> Page<T>(IReadOnlyList<T> items, int pageIndex, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        return items.Skip(pageIndex * pageSize).Take(pageSize).ToList();
    }
}
