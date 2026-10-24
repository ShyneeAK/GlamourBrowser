using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Items;

public static class ItemFilter
{
    /// <param name="categoriesOf">Source categories per item; null while the source index is not ready (category filter ignored).</param>
    /// <param name="patchOf">Patch per item (0 = unknown); null ignores the expansion filter and sorts by level.</param>
    public static IReadOnlyList<GlamourItem> Apply(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
                                                   Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null)
    {
        var search = filter.SearchText.Trim();
        var filtered = items
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
            .Where(i => categoriesOf == null || filter.Categories.Count == 0 || categoriesOf(i.Id).Overlaps(filter.Categories))
            .Where(i => patchOf == null || filter.Expansions.Count == 0
                        || Expansions.FromPatch(patchOf(i.Id)) is { } expansion && filter.Expansions.Contains(expansion));

        if (filter.Sort == SortOrder.Newest && patchOf != null)
        {
            // Unknown patch (0) sorts last.
            return filtered
                .OrderByDescending(i => patchOf(i.Id) > 0)
                .ThenByDescending(i => patchOf(i.Id))
                .ThenByDescending(i => i.Id)
                .ToList();
        }

        return filtered
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
