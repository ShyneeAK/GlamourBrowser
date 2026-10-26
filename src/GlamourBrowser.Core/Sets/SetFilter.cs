using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Sets;

/// <summary>
/// The filters of the set mode: like <see cref="ItemFilter"/>, but a set matches the jobs when every armour piece is made for them,
/// counts as owned only when complete, has the categories of all its pieces and the patch of its newest piece.
/// Slots and dye channels do not apply to sets.
/// </summary>
public static class SetFilter
{
    /// <returns>The sorted results, and every set that passes all filters except the source categories (for the counts).</returns>
    public static (IReadOnlyList<SetSubject> Results, IReadOnlyList<SetSubject> WithoutCategories) Apply(
        IEnumerable<SetSubject> sets, FilterState filter, Func<uint, bool> isOwned, byte race, byte sex,
        Func<uint, IReadOnlySet<SourceCategory>>? categoriesOf = null, Func<uint, float>? patchOf = null, Func<uint, uint>? parentOf = null)
    {
        var search = filter.SearchText.Trim();
        var jobs = new JobFilter(filter.Jobs, parentOf ?? (job => job), filter.JobsExclusive);
        var withoutCategories = sets
            .Where(s => search.Length == 0 || s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || s.Pieces.Any(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(s => filter.Jobs.Count == 0 || s.JobPieces.All(p => jobs.Fits(p.ClassJobIds)))
            .Where(s => s.Set.Level >= filter.MinLevel && s.Set.Level <= filter.MaxLevel)
            .Where(s => filter.Ownership switch
            {
                OwnershipFilter.Owned => SetProgress.Of(s, isOwned).Complete,
                OwnershipFilter.Missing => !SetProgress.Of(s, isOwned).Complete,
                _ => true,
            })
            .Where(s => !filter.WearableOnly || s.Allows(race, sex))
            .Where(s => patchOf == null || filter.Expansions.Count == 0
                        || Expansions.FromPatch(PatchOf(s, patchOf)) is { } expansion && filter.Expansions.Contains(expansion))
            .ToList();
        var results = withoutCategories.Where(s => categoriesOf == null || filter.Categories.Count == 0 || CategoriesOf(s, categoriesOf).Overlaps(filter.Categories));
        return (Sort(results, filter.Sort, patchOf), withoutCategories);
    }

    /// <summary>Every category any piece has.</summary>
    public static IReadOnlySet<SourceCategory> CategoriesOf(SetSubject set, Func<uint, IReadOnlySet<SourceCategory>> categoriesOf)
        => set.Pieces.SelectMany(p => categoriesOf(p.Id)).ToHashSet();

    /// <summary>The patch of the newest piece (when the set became complete), 0 when unknown.</summary>
    public static float PatchOf(SetSubject set, Func<uint, float> patchOf) => set.Pieces.Max(p => patchOf(p.Id));

    /// <summary>Number of sets per category; each set counts at most once per category.</summary>
    public static IReadOnlyDictionary<SourceCategory, int> Counts(IEnumerable<SetSubject> sets, Func<uint, IReadOnlySet<SourceCategory>> categoriesOf)
    {
        var counts = new Dictionary<SourceCategory, int>();
        foreach (var set in sets)
        {
            foreach (var category in CategoriesOf(set, categoriesOf))
                counts[category] = counts.GetValueOrDefault(category) + 1;
        }
        return counts;
    }

    private static IReadOnlyList<SetSubject> Sort(IEnumerable<SetSubject> sets, SortOrder sort, Func<uint, float>? patchOf)
        => Sorting.Apply(sets, sort, s => s.Set.Level, s => s.Name, patchOf == null ? null : s => PatchOf(s, patchOf));
}
