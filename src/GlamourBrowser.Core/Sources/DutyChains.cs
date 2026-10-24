namespace GlamourBrowser.Core.Sources;

public enum ChainKind
{
    Coffer,
    Token,
}

/// <param name="ViaItemId">The coffer or token the duty drops.</param>
public sealed record ChainSource(uint ItemId, uint DutyId, uint ViaItemId, ChainKind Kind);

/// <summary>
/// Resolves duty drops one step further: coffers to their contents and tokens to the gear they buy.
/// One result per item and duty; an item the duty drops directly is never reported again as a chain.
/// </summary>
public static class DutyChains
{
    public static IReadOnlyList<ChainSource> Resolve(
        IEnumerable<(uint DutyId, uint ItemId)> drops,
        IReadOnlyDictionary<uint, IReadOnlyList<uint>> cofferContents,
        IReadOnlyDictionary<uint, IReadOnlyList<uint>> tokenRewards,
        IReadOnlySet<uint> relevant)
    {
        var dropList = drops.ToList();
        var seen = dropList.ToHashSet();
        var result = new List<ChainSource>();

        void Add(uint duty, uint via, ChainKind kind, IReadOnlyList<uint> items)
        {
            foreach (var item in items)
            {
                if (relevant.Contains(item) && seen.Add((duty, item)))
                    result.Add(new ChainSource(item, duty, via, kind));
            }
        }

        foreach (var (duty, via) in dropList)
        {
            if (cofferContents.TryGetValue(via, out var contents))
                Add(duty, via, ChainKind.Coffer, contents);
            if (tokenRewards.TryGetValue(via, out var rewards))
                Add(duty, via, ChainKind.Token, rewards);
        }
        return result;
    }
}
