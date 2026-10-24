using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using static GlamourBrowser.Core.Sources.SourceCategory;

namespace GlamourBrowser.Core.Tests;

public class ItemTraitsTests
{
    // Paladin 19, Warrior 21, Dark Knight 32, Gunbreaker 37 are tanks; Bard 23, White Mage 24.
    private static JobGroup GroupOf(uint job) => job switch
    {
        19 or 21 or 32 or 37 => JobGroup.Tank,
        24 => JobGroup.Healer,
        _ => JobGroup.Ranged,
    };

    private const int AllJobs = 30;

    [Theory]
    [InlineData(true, 0u, Tradability.Bound)]
    [InlineData(false, 0u, Tradability.Tradable)]
    [InlineData(false, 12u, Tradability.Marketable)]
    public void Tradability_From(bool untradable, uint search, Tradability expected)
        => Assert.Equal(expected, Tradabilities.From(untradable, search));

    [Fact]
    public void JobBadge_SingleJob() => Assert.Equal(new JobBadge(JobBadgeKind.Job, 19, JobGroup.Tank), JobBadges.For(new HashSet<uint> { 19 }, GroupOf, AllJobs));

    [Fact]
    public void JobBadge_OneRole() => Assert.Equal(new JobBadge(JobBadgeKind.Role, 0, JobGroup.Tank), JobBadges.For(new HashSet<uint> { 19, 21, 32, 37 }, GroupOf, AllJobs));

    [Fact]
    public void JobBadge_Mixed_None() => Assert.Equal(JobBadgeKind.None, JobBadges.For(new HashSet<uint> { 19, 24 }, GroupOf, AllJobs).Kind);

    [Fact]
    public void JobBadge_AllOrEmpty_None()
    {
        Assert.Equal(JobBadgeKind.None, JobBadges.For(new HashSet<uint>(), GroupOf, AllJobs).Kind);
        var all = Enumerable.Range(1, AllJobs).Select(i => (uint)i).ToHashSet();
        Assert.Equal(JobBadgeKind.None, JobBadges.For(all, GroupOf, AllJobs).Kind);
    }

    [Fact]
    public void Icons_OnePerGroupInGroupOrder()
        => Assert.Equal([RaidNormal, Crafting, Tomestones, GilVendor],
            SourceRanking.Icons(new HashSet<SourceCategory> { GilVendor, Dungeon, Tomestones, RaidNormal, Crafting }));

    [Fact]
    public void Icons_EmptyCategories()
    {
        Assert.Empty(SourceRanking.Icons(new HashSet<SourceCategory>()));
        Assert.Null(SourceRanking.Top(new HashSet<SourceCategory>()));
    }

    [Fact]
    public void Top_SavageBeatsVendor() => Assert.Equal(RaidSavage, SourceRanking.Top(new HashSet<SourceCategory> { GilVendor, RaidSavage }));

    [Fact]
    public void JobBadge_ClassAndItsJob_IsTheJob()
    {
        // Conjurer 6 is the base class of White Mage 24 (ClassJobParent of 24 is 6).
        static uint ParentOf(uint job) => job == 24 ? 6u : job;
        var jobs = JobBadges.CollapseClasses(new HashSet<uint> { 6, 24 }, ParentOf);
        Assert.Equal(new JobBadge(JobBadgeKind.Job, 24, JobGroup.Healer), JobBadges.For(jobs, GroupOf, AllJobs));
    }
}
