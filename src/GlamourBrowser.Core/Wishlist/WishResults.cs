using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Wishlist;

/// <summary>What the grid shows in the wishlist mode.</summary>
public static class WishResults
{
    /// <summary>
    /// The entries as items and sets, newest first. Entries that do not resolve (item not in the catalog, set not
    /// detected or not yet loaded) are left out; they stay on the list.
    /// </summary>
    public static IReadOnlyList<TileSubject> Resolve(IReadOnlyList<WishEntry> entries, Func<uint, TileSubject?> itemOf, Func<SetId, TileSubject?> setOf)
        => entries
            .Reverse()
            .Select(e => e.Key.Kind switch
            {
                WishKind.Item => itemOf(e.Key.ItemId),
                WishKind.Set => setOf(e.Key.Set),
                _ => null,
            })
            .OfType<TileSubject>()
            .ToList();

    /// <summary>Only the search (the subject's name or a piece's) and "Erledigte ausblenden" apply to the wishlist.</summary>
    public static IReadOnlyList<TileSubject> Filter(IReadOnlyList<TileSubject> subjects, string searchText, bool hideDone, Func<uint, bool> isOwned)
    {
        var search = searchText.Trim();
        return subjects
            .Where(s => search.Length == 0 || s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || s.Wear.Any(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(s => !hideDone || !WishStatus.IsDone(s, isOwned))
            .ToList();
    }
}
