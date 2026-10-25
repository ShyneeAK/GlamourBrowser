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

/// <param name="Source">The piece's own source in its group: cost and action can differ per piece (price, recipe, store product); null for "Quelle unbekannt".</param>
public sealed record WishPiece(GlamourItem Item, ItemSource? Source);

/// <param name="Source">The first source with this category, title and detail (for the header); null for "Quelle unbekannt".</param>
/// <param name="Pieces">The missing pieces this source gives, by slot, then name.</param>
/// <param name="SharedAction">The action every piece has (one map flag, one duty); null when they differ, then each piece has its own.</param>
public sealed record WishGroup(ItemSource? Source, IReadOnlyList<WishPiece> Pieces, SourceAction? SharedAction);

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

        var groups = new Dictionary<(SourceCategory, string, string), (ItemSource Source, List<WishPiece> Pieces)>();
        var unknown = new List<WishPiece>();
        foreach (var piece in missing)
        {
            var sources = sourcesOf(piece.Id);
            if (sources.Count == 0)
                unknown.Add(new WishPiece(piece, null));
            foreach (var source in sources)
            {
                var key = (source.Category, source.Title, source.Detail);
                if (!groups.TryGetValue(key, out var group))
                    groups[key] = group = (source, new List<WishPiece>());
                // Two sources of one piece with the same key put it in the group once, with the first of them.
                if (group.Pieces.Count == 0 || group.Pieces[^1].Item.Id != piece.Id)
                    group.Pieces.Add(new WishPiece(piece, source));
            }
        }

        var result = groups.Values
            .OrderByDescending(g => g.Pieces.Count)
            .ThenBy(g => SourceRanking.Rank(g.Source.Category))
            .ThenBy(g => g.Source.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Source.Detail, StringComparer.OrdinalIgnoreCase)
            .Select(g => new WishGroup(g.Source, Ordered(g.Pieces), SharedAction(g.Pieces)))
            .ToList();
        if (unknown.Count > 0)
            result.Add(new WishGroup(null, Ordered(unknown), null));
        return new WishPlan(missing.Count, pieces.Count, result);
    }

    private static SourceAction? SharedAction(List<WishPiece> pieces)
    {
        var first = pieces[0].Source!.Action;
        return pieces.All(p => Equals(p.Source!.Action, first)) ? first : null;
    }

    private static IReadOnlyList<WishPiece> Ordered(IEnumerable<WishPiece> pieces)
        => pieces.OrderBy(p => p.Item.Slot).ThenBy(p => p.Item.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
