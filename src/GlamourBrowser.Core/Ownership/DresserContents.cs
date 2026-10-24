namespace GlamourBrowser.Core.Ownership;

public static class DresserContents
{
    /// <summary>Dresser ids above this value are high-quality items.</summary>
    public const uint HqOffset = 1_000_000;

    /// <summary>Turns raw glamour dresser ids into item ids; outfit entries also count as all items they contain.</summary>
    public static IReadOnlyList<uint> Resolve(IEnumerable<uint> rawIds, IReadOnlyDictionary<uint, IReadOnlyList<uint>> outfitSets)
    {
        var result = new HashSet<uint>();
        foreach (var raw in rawIds)
        {
            if (raw == 0)
                continue;

            var id = raw > HqOffset ? raw - HqOffset : raw;
            result.Add(id);
            if (outfitSets.TryGetValue(id, out var contained))
                result.UnionWith(contained);
        }
        return result.ToList();
    }
}
