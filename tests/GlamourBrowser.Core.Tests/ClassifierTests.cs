using GlamourBrowser.Core.Sources;
using static GlamourBrowser.Core.Sources.SourceCategory;

namespace GlamourBrowser.Core.Tests;

public class ClassifierTests
{
    // TomestonesItem item ids from the game data: 23, 24, 26, 28, 30–49 (25 Wolf Mark, 27 Allied Seal, 29 MGP are not tomestones).
    private static readonly IReadOnlySet<uint> TomestoneIds = new uint[] { 23, 24, 26, 28 }.Concat(Enumerable.Range(30, 20).Select(i => (uint)i)).ToHashSet();

    [Theory]
    [InlineData(2u, 3u, "", "", Dungeon)]
    [InlineData(4u, 3u, "", "", Trial)]
    [InlineData(28u, 3u, "Futures Rewritten (Ultimate)", "", RaidUltimate)]
    [InlineData(37u, 33u, "the Cloud of Darkness (Chaotic)", "", RaidAlliance)]
    [InlineData(5u, 4u, "Jeuno: The First Walk", "", RaidAlliance)]
    [InlineData(5u, 3u, "AAC Light-heavyweight M4 (Savage)", "", RaidSavage)]
    [InlineData(5u, 3u, "", "Arkadion - Halbschwergewicht R4 (episch)", RaidSavage)]
    [InlineData(5u, 3u, "AAC Light-heavyweight M4", "", RaidNormal)]
    [InlineData(21u, 3u, "", "", OtherContent)]
    public void Duty(uint type, uint member, string en, string de, SourceCategory expected)
        => Assert.Equal(expected, DutyCategory.From(type, member, en, de));

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
    public void Currency(uint item, SourceCategory expected) => Assert.Equal(expected, CurrencyCategory.From(item, TomestoneIds));

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
            Assert.Equal(Scrips, CurrencyCategory.From(SpecialShopCost.ResolveItemId(id, 16, OrderedTomestones), TomestoneIds));
    }

    [Fact]
    public void SpecialShopCost_Type4TomestoneIndex() => Assert.Equal(48u, SpecialShopCost.ResolveItemId(2, 4, OrderedTomestones));

    [Theory]
    [InlineData(1u, 0)]
    [InlineData(2u, 1)]
    [InlineData(3u, 2)]
    public void SpecialShopCost_Type2AllCodesAreTomestones(uint code, int index)
        => Assert.Equal(OrderedTomestones[index], SpecialShopCost.ResolveItemId(code, 2, OrderedTomestones));

    [Fact]
    public void SpecialShopCost_AnyScripIsNotAnExactCurrency()
    {
        Assert.False(SpecialShopCost.IsExactCurrency(SpecialShopCost.AnyScrip));
        Assert.True(SpecialShopCost.IsExactCurrency(28));
    }

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
