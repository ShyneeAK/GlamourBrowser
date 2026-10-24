namespace GlamourBrowser.Core.Sources;

/// <summary>
/// SpecialShop cost ids 1–7 are codes that depend on the shop's UseCurrencyType rather than real items.
/// </summary>
public static class SpecialShopCost
{
    /// <summary>Stand-in for "some crafter/gatherer scrip": the exact scrip depends on runtime state.</summary>
    public const uint AnyScrip = 10308;

    private const uint Poetics = 28;

    /// <param name="orderedTomestoneItemIds">TomestonesItem rows with Tomestones.RowId &gt; 0, ordered by it; index 0 is code 1.</param>
    public static uint ResolveItemId(uint costItemId, byte useCurrencyType, IReadOnlyList<uint> orderedTomestoneItemIds)
    {
        if (costItemId is 0 or > 7)
            return costItemId;

        return useCurrencyType switch
        {
            16 => costItemId switch
            {
                1 => Poetics,
                2 or 4 or 6 or 7 => AnyScrip,
                _ => costItemId,
            },
            8 => CurrencyCategory.Gil,
            4 => Tomestone(costItemId, orderedTomestoneItemIds),
            2 => Tomestone(costItemId, orderedTomestoneItemIds),
            _ => costItemId,
        };
    }

    /// <summary>False for <see cref="AnyScrip"/>: it only names the category, not the scrip actually paid.</summary>
    public static bool IsExactCurrency(uint itemId) => itemId != AnyScrip;

    private static uint Tomestone(uint code, IReadOnlyList<uint> ordered)
        => code <= ordered.Count ? ordered[(int)code - 1] : code;
}
