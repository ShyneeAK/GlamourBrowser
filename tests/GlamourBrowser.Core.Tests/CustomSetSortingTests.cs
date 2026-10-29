using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class CustomSetSortingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(1));

    private static SetSubject Set(uint id, string name, int level)
        => new(new GlamourSet(new SetId(SetKind.Custom, id), name, [], level), []);

    // Changed: Robe yesterday, Anzug today, Ärmel and Zylinder an hour ago (the same time).
    private static readonly SetSubject[] Sets = [Set(1, "Robe", 90), Set(2, "Anzug", 50), Set(3, "Ärmel", 70), Set(4, "Zylinder", 70)];

    private static DateTimeOffset ChangedOf(uint id) => id switch { 1 => Now.AddDays(-1), 2 => Now, _ => Now.AddHours(-1) };

    private static string[] Names(SortOrder sort) => CustomSetSorting.Apply(Sets, sort, ChangedOf).Select(s => s.Name).ToArray();

    [Fact]
    public void Newest_LastChangedFirst_TheSameTimeTheLaterSetFirst()
        => Assert.Equal(["Anzug", "Zylinder", "Ärmel", "Robe"], Names(SortOrder.Newest));

    [Fact]
    public void Oldest_FirstChangedFirst()
        => Assert.Equal(["Robe", "Ärmel", "Zylinder", "Anzug"], Names(SortOrder.Oldest));

    [Fact]
    public void Name_InTheAlphabet_ÄWithA()
    {
        Assert.Equal(["Anzug", "Ärmel", "Robe", "Zylinder"], Names(SortOrder.NameAscending));
        Assert.Equal(["Zylinder", "Robe", "Ärmel", "Anzug"], Names(SortOrder.NameDescending));
    }

    [Fact]
    public void Level_ByTheHighestPiece_ThenByName()
    {
        Assert.Equal(["Robe", "Ärmel", "Zylinder", "Anzug"], Names(SortOrder.Level));
        Assert.Equal(["Anzug", "Ärmel", "Zylinder", "Robe"], Names(SortOrder.LevelAscending));
    }
}
