using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class DutyChainsTests
{
    private static readonly IReadOnlyDictionary<uint, IReadOnlyList<uint>> None = new Dictionary<uint, IReadOnlyList<uint>>();

    private static IReadOnlyDictionary<uint, IReadOnlyList<uint>> Map(uint key, params uint[] values)
        => new Dictionary<uint, IReadOnlyList<uint>> { [key] = values };

    private static HashSet<uint> Relevant(params uint[] ids) => ids.ToHashSet();

    [Fact]
    public void Resolve_CofferContentsBecomeSources()
    {
        var result = DutyChains.Resolve([(10u, 900u)], Map(900, 1, 2), None, Relevant(1, 2));
        Assert.Equal([new ChainSource(1, 10, 900, ChainKind.Coffer), new ChainSource(2, 10, 900, ChainKind.Coffer)], result);
    }

    [Fact]
    public void Resolve_TokenRewardsBecomeSources()
    {
        var result = DutyChains.Resolve([(10u, 800u)], None, Map(800, 3), Relevant(3));
        Assert.Equal([new ChainSource(3, 10, 800, ChainKind.Token)], result);
    }

    [Fact]
    public void Resolve_DirectDropWins_OnePerDuty()
    {
        var result = DutyChains.Resolve([(10u, 1u), (10u, 900u), (10u, 800u)], Map(900, 1, 2), Map(800, 2), Relevant(1, 2));
        Assert.Equal([new ChainSource(2, 10, 900, ChainKind.Coffer)], result);
    }

    [Fact]
    public void Resolve_SameTokenInFourDuties_FourSources()
    {
        var result = DutyChains.Resolve([(10u, 800u), (11u, 800u), (12u, 800u), (13u, 800u)], None, Map(800, 3), Relevant(3));
        Assert.Equal([10u, 11u, 12u, 13u], result.Select(r => r.DutyId));
    }

    [Fact]
    public void Resolve_IgnoresIrrelevantItems()
    {
        var result = DutyChains.Resolve([(10u, 900u)], Map(900, 1, 2), None, Relevant(2));
        Assert.Equal([2u], result.Select(r => r.ItemId));
    }
}
