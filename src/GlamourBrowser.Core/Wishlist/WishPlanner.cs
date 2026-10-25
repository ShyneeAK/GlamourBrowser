using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Wishlist;

public static class WishStatus
{
    /// <summary>An item wish is done when the item is owned, a set wish when every piece is.</summary>
    public static bool IsDone(TileSubject subject, Func<uint, bool> isOwned) => subject switch
    {
        ItemSubject { Item: var item } => isOwned(item.Id),
        SetSubject set => SetProgress.Of(set, isOwned).Complete,
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };
}

/// <param name="Source">The first source with this category, title and detail (its action and cost); null for "Quelle unbekannt".</param>
/// <param name="Pieces">The missing pieces this source gives, by slot, then name.</param>
public sealed record WishGroup(ItemSource? Source, IReadOnlyList<GlamourItem> Pieces);

/// <param name="Missing">Different pieces not owned yet.</param>
/// <param name="Total">Different pieces of all entries, done ones included.</param>
public sealed record WishPlan(int Missing, int Total, IReadOnlyList<WishGroup> Groups);

/// <summary>The missing pieces of a wishlist grouped by where they come from, to see where a run pays off.</summary>
public static class WishPlanner
{
    /// <param name="entries">Every resolved entry, done ones included; a piece in several entries counts once.</param>
    public static WishPlan Plan(IReadOnlyList<TileSubject> entries, Func<uint, bool> isOwned, Func<uint, IReadOnlyList<ItemSource>> sourcesOf)
    {
        var pieces = new Dictionary<uint, GlamourItem>();
        foreach (var piece in entries.SelectMany(e => e.Wear))
            pieces.TryAdd(piece.Id, piece);
        var missing = pieces.Values.Where(p => !isOwned(p.Id)).ToList();

        var groups = new Dictionary<(SourceCategory, string, string), (ItemSource Source, List<GlamourItem> Pieces)>();
        var unknown = new List<GlamourItem>();
        foreach (var piece in missing)
        {
            var sources = sourcesOf(piece.Id);
            if (sources.Count == 0)
                unknown.Add(piece);
            foreach (var source in sources)
            {
                var key = (source.Category, source.Title, source.Detail);
                if (!groups.TryGetValue(key, out var group))
                    groups[key] = group = (source, new List<GlamourItem>());
                // Two sources of one piece with the same key put it in the group once.
                if (group.Pieces.Count == 0 || group.Pieces[^1].Id != piece.Id)
                    group.Pieces.Add(piece);
            }
        }

        var result = groups.Values
            .OrderByDescending(g => g.Pieces.Count)
            .ThenBy(g => SourceRanking.Rank(g.Source.Category))
            .ThenBy(g => g.Source.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Source.Detail, StringComparer.OrdinalIgnoreCase)
            .Select(g => new WishGroup(g.Source, Ordered(g.Pieces)))
            .ToList();
        if (unknown.Count > 0)
            result.Add(new WishGroup(null, Ordered(unknown)));
        return new WishPlan(missing.Count, pieces.Count, result);
    }

    private static IReadOnlyList<GlamourItem> Ordered(IEnumerable<GlamourItem> pieces)
        => pieces.OrderBy(p => p.Slot).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
