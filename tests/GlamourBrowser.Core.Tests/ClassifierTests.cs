using GlamourBrowser.Core.Sources;
using static GlamourBrowser.Core.Sources.SourceCategory;

namespace GlamourBrowser.Core.Tests;

public class ClassifierTests
{
    private static readonly IReadOnlySet<uint> Tomestones = Enumerable.Range(23, 27).Select(i => (uint)i).ToHashSet();

    [Theory]
    [InlineData(2u, Dungeon)]
    [InlineData(4u, Trial)]
    [InlineData(5u, Raid)]
    [InlineData(28u, Raid)]
    [InlineData(37u, Raid)]
    [InlineData(21u, OtherContent)]
    public void Duty(uint contentType, SourceCategory expected) => Assert.Equal(expected, DutyCategory.From(contentType));

    [Theory]
    [InlineData(28u, Tomestones)]
    [InlineData(25199u, Scrips)]
    [InlineData(41784u, Scrips)]
    [InlineData(25u, PvP)]
    [InlineData(36656u, PvP)]
    [InlineData(29u, GoldSaucer)]
    [InlineData(20u, GrandCompany)]
    [InlineData(1u, GilVendor)]
    [InlineData(26807u, Tokens)]
    public void Currency(uint item, SourceCategory expected) => Assert.Equal(expected, CurrencyCategory.From(item, Tomestones));

    [Theory]
    [InlineData(97u, SeasonalEvent)]
    [InlineData(93u, JobArtifact)]
    [InlineData(94u, JobArtifact)]
    [InlineData(56u, JobArtifact)]
    [InlineData(3u, Quest)]
    public void QuestJournal(uint category, SourceCategory expected) => Assert.Equal(expected, QuestCategory.From(category));

    private static readonly uint[] OrderedTomestones = [28, 48, 49, 47];

    [Fact]
    public void SpecialShopCost_Type16Poetics() => Assert.Equal(28u, SpecialShopCost.ResolveItemId(1, 16, OrderedTomestones));

    [Fact]
    public void SpecialShopCost_Type16ScripsAreScrips()
    {
        foreach (var id in new uint[] { 2, 4, 6, 7 })
            Assert.Equal(Scrips, CurrencyCategory.From(SpecialShopCost.ResolveItemId(id, 16, OrderedTomestones), Tomestones));
    }

    [Fact]
    public void SpecialShopCost_Type4TomestoneIndex() => Assert.Equal(48u, SpecialShopCost.ResolveItemId(2, 4, OrderedTomestones));

    [Fact]
    public void SpecialShopCost_Type8IsGil() => Assert.Equal(1u, SpecialShopCost.ResolveItemId(3, 8, OrderedTomestones));

    [Fact]
    public void SpecialShopCost_RealItemUnchanged() => Assert.Equal(26807u, SpecialShopCost.ResolveItemId(26807, 4, OrderedTomestones));

    [Theory]
    [InlineData(2.0f, Expansion.ARealmReborn)]
    [InlineData(3.5f, Expansion.Heavensward)]
    [InlineData(6.45f, Expansion.Endwalker)]
    [InlineData(7.1f, Expansion.Dawntrail)]
    [InlineData(8.0f, Expansion.Dawntrail)]
    public void ExpansionFromPatch(float patch, Expansion expected) => Assert.Equal(expected, Expansions.FromPatch(patch));

    [Fact]
    public void ExpansionFromPatch_ZeroIsUnknown() => Assert.Null(Expansions.FromPatch(0));
}
