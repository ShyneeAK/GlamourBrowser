namespace GlamourBrowser.Core.Items;

public static class ItemFilter
{
    public static IReadOnlyList<GlamourItem> Apply(IEnumerable<GlamourItem> items, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex)
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
            .OrderByDescending(i => i.LevelEquip)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int PageCount(int itemCount, int pageSize)
        => Math.Max(1, (itemCount + pageSize - 1) / pageSize);

    public static int ClampPage(int pageIndex, int itemCount, int pageSize)
        => Math.Clamp(pageIndex, 0, PageCount(itemCount, pageSize) - 1);

    public static IReadOnlyList<GlamourItem> Page(IReadOnlyList<GlamourItem> items, int pageIndex, int pageSize)
        => items.Skip(pageIndex * pageSize).Take(pageSize).ToList();
}
