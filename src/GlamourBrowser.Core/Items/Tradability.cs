namespace GlamourBrowser.Core.Items;

public enum Tradability
{
    Bound,
    Tradable,
    Marketable,
}

public static class Tradabilities
{
    /// <summary>Marketable when the item has a market board category, otherwise tradable unless it is untradable.</summary>
    public static Tradability From(bool isUntradable, uint itemSearchCategory)
    {
        if (isUntradable)
            return Tradability.Bound;
        return itemSearchCategory != 0 ? Tradability.Marketable : Tradability.Tradable;
    }
}
