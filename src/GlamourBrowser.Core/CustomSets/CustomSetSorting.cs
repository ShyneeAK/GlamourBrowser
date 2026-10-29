using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>The order of "Meine Sets": newest and oldest by the date a set was last changed, level by its highest piece, or by name.</summary>
public static class CustomSetSorting
{
    /// <param name="changedOf">When a custom set (by id) was last changed.</param>
    public static IReadOnlyList<SetSubject> Apply(IEnumerable<SetSubject> sets, SortOrder sort, Func<uint, DateTimeOffset> changedOf)
        => sort switch
        {
            // The same date: the set made later first (or last).
            SortOrder.Newest => sets.OrderByDescending(s => changedOf(s.Set.Id.Value)).ThenByDescending(s => s.Set.Id.Value).ToList(),
            SortOrder.Oldest => sets.OrderBy(s => changedOf(s.Set.Id.Value)).ThenBy(s => s.Set.Id.Value).ToList(),
            _ => Sorting.Apply(sets, sort, s => s.Set.Level, s => s.Name, patch: null),
        };
}
