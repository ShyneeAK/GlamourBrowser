namespace GlamourBrowser.Core.Sources;

/// <summary>Category of a shop by the currency it asks for.</summary>
public static class CurrencyCategory
{
    public const uint Gil = 1;

    private static readonly HashSet<uint> Scrips = [10308, 10309, 10310, 10311, 17833, 17834, 25199, 25200, 28063, 33913, 33914, 41784, 41785];
    private static readonly HashSet<uint> PvP = [25, 21067, 36656, 40479];
    private static readonly HashSet<uint> GoldSaucer = [29, 41629, 52322];
    private static readonly HashSet<uint> GrandCompanySeals = [20, 21, 22];

    /// <param name="tomestoneItemIds">Item ids from the TomestonesItem sheet.</param>
    public static SourceCategory From(uint currencyItemId, IReadOnlySet<uint> tomestoneItemIds)
    {
        // Fixed lists first: they are certain, the tomestone set comes from data.
        if (currencyItemId == Gil)
            return SourceCategory.GilVendor;
        if (Scrips.Contains(currencyItemId))
            return SourceCategory.Scrips;
        if (PvP.Contains(currencyItemId))
            return SourceCategory.PvP;
        if (GoldSaucer.Contains(currencyItemId))
            return SourceCategory.GoldSaucer;
        if (GrandCompanySeals.Contains(currencyItemId))
            return SourceCategory.GrandCompany;
        if (tomestoneItemIds.Contains(currencyItemId))
            return SourceCategory.Tomestones;
        return SourceCategory.Tokens;
    }

    /// <summary>
    /// Category of a SpecialShop entry. Free entries (amount 0: re-obtaining job gear, achievement rewards, replicas)
    /// are no purchase, whatever currency code they carry.
    /// </summary>
    /// <param name="jobGear">The item is wearable by a single job.</param>
    public static SourceCategory ForSpecialShop(uint currencyItemId, uint amount, bool jobGear, IReadOnlySet<uint> tomestoneItemIds)
        => amount == 0
            ? jobGear ? SourceCategory.JobGear : SourceCategory.Other
            : From(currencyItemId, tomestoneItemIds);
}
