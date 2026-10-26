using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Sources;

/// <summary>Orders items or sets after a <see cref="SortOrder"/>; names compare in the language's alphabet (Ä with A).</summary>
public static class Sorting
{
    /// <param name="patch">The patch (0 = unknown); null sorts the patch orders by level instead.</param>
    /// <param name="id">Order among the same patch, by id (newest first: the highest); null orders them by name.</param>
    public static IReadOnlyList<T> Apply<T>(IEnumerable<T> subjects, SortOrder sort, Func<T, int> level, Func<T, string> name,
                                            Func<T, float>? patch, Func<T, uint>? id = null)
    {
        var names = StringComparer.Create(Loc.Culture, ignoreCase: true);
        if (sort is SortOrder.Newest or SortOrder.Oldest && patch != null)
        {
            var newest = sort == SortOrder.Newest;
            // Unknown patch (0) sorts last either way.
            var known = subjects.Select(s => (Subject: s, Patch: patch(s))).OrderByDescending(x => x.Patch > 0);
            var byPatch = newest ? known.ThenByDescending(x => x.Patch) : known.ThenBy(x => x.Patch);
            var tied = id == null ? byPatch.ThenBy(x => name(x.Subject), names)
                : newest ? byPatch.ThenByDescending(x => id(x.Subject)) : byPatch.ThenBy(x => id(x.Subject));
            return tied.Select(x => x.Subject).ToList();
        }

        return sort switch
        {
            SortOrder.LevelAscending => subjects.OrderBy(level).ThenBy(name, names).ToList(),
            SortOrder.NameAscending => subjects.OrderBy(name, names).ToList(),
            SortOrder.NameDescending => subjects.OrderByDescending(name, names).ToList(),
            _ => subjects.OrderByDescending(level).ThenBy(name, names).ToList(),
        };
    }
}
