namespace GlamourBrowser.Core.Items;

/// <summary>What the grid shows, chosen in the slot bar: items (by slot), detected sets, the custom sets or the wishlist.</summary>
public enum BrowseMode
{
    Items,
    Sets,
    MySets,
    Wishlist,
}

public static class BrowseModes
{
    /// <summary>Modes in which only the search applies; the other filters keep their state for later.</summary>
    public static bool OnlySearch(BrowseMode mode) => mode is BrowseMode.MySets or BrowseMode.Wishlist;

    /// <summary>Why the other filters are grayed out in an <see cref="OnlySearch"/> mode.</summary>
    public static string NotApplicable(BrowseMode mode) => mode == BrowseMode.MySets ? "gilt nicht für Meine Sets" : "gilt nicht für die Wunschliste";

    public static string DisplayName(BrowseMode mode) => mode switch
    {
        BrowseMode.Sets => "Ausrüstungs-Sets",
        BrowseMode.MySets => "Meine Sets",
        BrowseMode.Wishlist => "Wunschliste",
        _ => "Items",
    };
}
