using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Items;

/// <summary>
/// What the grid shows, chosen in the slot bar: items (by slot), detected sets, the custom sets, the wishlist, the items
/// clicked last or the favourites.
/// </summary>
public enum BrowseMode
{
    Items,
    Sets,
    MySets,
    Wishlist,
    History,
    Favorites,
}

public static class BrowseModes
{
    /// <summary>Modes in which only the search applies; the other filters keep their state for later.</summary>
    public static bool OnlySearch(BrowseMode mode) => mode is BrowseMode.MySets or BrowseMode.Wishlist;

    /// <summary>Modes that show a list of items in its own order (newest first): every filter applies, the sort does not.</summary>
    public static bool ItemList(BrowseMode mode) => mode is BrowseMode.History or BrowseMode.Favorites;

    /// <summary>Why the other filters are grayed out in an <see cref="OnlySearch"/> mode.</summary>
    public static string NotApplicable(BrowseMode mode) => Loc.T(mode == BrowseMode.MySets ? "gilt nicht für Meine Sets" : "gilt nicht für die Wunschliste");

    public static string DisplayName(BrowseMode mode) => Loc.T(mode switch
    {
        BrowseMode.Sets => "Ausrüstungs-Sets",
        BrowseMode.MySets => "Meine Sets",
        BrowseMode.Wishlist => "Wunschliste",
        BrowseMode.History => "Zuletzt benutzt",
        BrowseMode.Favorites => "Favoriten",
        _ => "Items",
    });
}
