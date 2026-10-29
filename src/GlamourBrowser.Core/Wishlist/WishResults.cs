using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
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
        var search = SearchQuery.Parse(searchText);
        return subjects
            .Where(s => search.Matches(s.Wear.Select(p => p.Name).Prepend(s.Name)))
            .Where(s => !hideDone || !WishStatus.IsDone(s, isOwned))
            .ToList();
    }

    /// <summary>What an empty wishlist grid says; null when it shows tiles, or while the sets load (the grid says that already).</summary>
    /// <param name="resolved">Entries that resolved to an item or set.</param>
    /// <param name="shown">Entries left after the search and "Erledigte ausblenden".</param>
    public static string? EmptyHint(int resolved, int shown, string searchText, bool hideDone, bool loading)
    {
        if (shown > 0 || resolved == 0 && loading)
            return null;
        if (resolved == 0)
            return Loc.T("Keine Einträge im Katalog gefunden");
        if (searchText.Trim().Length > 0)
            return Loc.T("Keine Treffer");
        return hideDone ? Loc.T("Alles erledigt") : null;
    }
}
