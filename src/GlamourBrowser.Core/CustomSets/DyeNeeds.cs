namespace GlamourBrowser.Core.CustomSets;

/// <param name="ItemId">The item that gives the dye (e.g. "Einfacher Farbstoff").</param>
public readonly record struct DyeNeed(uint ItemId, int Count);

/// <summary>A piece's dyes, how many dye channels its item has and the dyes its copy in the dresser already has.</summary>
public readonly record struct DyedPiece(byte Stain0, byte Stain1, int DyeCount, byte Have0 = 0, byte Have1 = 0);

/// <param name="Total">Every dyed channel.</param>
/// <param name="Done">Channels whose dresser copy already has the dye.</param>
/// <param name="Needs">The dye items for the other channels, the most needed first.</param>
public sealed record DyeTally(int Total, int Done, IReadOnlyList<DyeNeed> Needs);

/// <summary>The dye items an outfit or set takes: most dyes are the generic "Einfacher Farbstoff", some still have their own.</summary>
public static class DyeNeeds
{
    /// <summary>
    /// One per dyed channel the item has, by the item the plate editor takes for the dye; a channel already dyed so in the
    /// dresser counts as done instead.
    /// </summary>
    /// <param name="dyeItemsOf">The items that give a dye (Stain sheet), the one the game takes first.</param>
    public static DyeTally Of(IEnumerable<DyedPiece> pieces, Func<byte, IReadOnlyList<uint>> dyeItemsOf)
    {
        var channels = pieces
            .SelectMany(p => new[] { (Want: p.DyeCount >= 1 ? p.Stain0 : (byte)0, Have: p.Have0), (Want: p.DyeCount >= 2 ? p.Stain1 : (byte)0, Have: p.Have1) })
            .Where(c => c.Want != 0 && dyeItemsOf(c.Want).Count > 0)
            .ToList();
        var needs = channels
            .Where(c => c.Have != c.Want)
            .GroupBy(c => dyeItemsOf(c.Want)[0])
            .Select(g => new DyeNeed(g.Key, g.Count()))
            .OrderByDescending(n => n.Count)
            .ThenBy(n => n.ItemId)
            .ToList();
        return new DyeTally(channels.Count, channels.Count(c => c.Have == c.Want), needs);
    }
}
