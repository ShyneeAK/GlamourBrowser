using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>The order of "Meine Sets": newest and oldest by the date a set was last changed, level by its highest piece, or by name.</summary>
public static class CustomSetSorting
{
    /// <param name="changedOf">When a custom set (by id) was last changed.</param>
    public static IReadOnlyList<SetSubject> Apply(IEnumerable<SetSubject> sets, SortOrder sort, Func<uint, DateTimeOffset> changedOf)
        => Sorting.ByDate(sets, sort, s => changedOf(s.Set.Id.Value), s => s.Set.Level, s => s.Name, s => s.Set.Id.Value);
}
