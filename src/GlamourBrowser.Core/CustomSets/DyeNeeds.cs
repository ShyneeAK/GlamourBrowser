namespace GlamourBrowser.Core.CustomSets;

/// <param name="ItemId">The item that gives the dye (e.g. "Einfacher Farbstoff").</param>
public readonly record struct DyeNeed(uint ItemId, int Count);

/// <summary>The dye items an outfit or set takes: most dyes are the generic "Einfacher Farbstoff", some still have their own.</summary>
public static class DyeNeeds
{
    /// <summary>One per dyed channel the item has, by the item the plate editor takes for the dye; the most needed first.</summary>
    /// <param name="pieces">The pieces' dyes and how many dye channels each item has.</param>
    /// <param name="dyeItemsOf">The items that give a dye (Stain sheet), the one the game takes first.</param>
    public static IReadOnlyList<DyeNeed> Of(IEnumerable<(byte Stain0, byte Stain1, int DyeCount)> pieces, Func<byte, IReadOnlyList<uint>> dyeItemsOf)
        => pieces
            .SelectMany(p => new[] { p.DyeCount >= 1 ? p.Stain0 : (byte)0, p.DyeCount >= 2 ? p.Stain1 : (byte)0 })
            .Where(stain => stain != 0)
            .Select(stain => dyeItemsOf(stain) is [var item, ..] ? item : 0)
            .Where(item => item != 0)
            .GroupBy(item => item)
            .Select(g => new DyeNeed(g.Key, g.Count()))
            .OrderByDescending(n => n.Count)
            .ThenBy(n => n.ItemId)
            .ToList();
}
