using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class JobGroupsTests
{
    [Theory]
    [InlineData((byte)1, 30u, (byte)1, JobGroup.Tank)]
    [InlineData((byte)4, 31u, (byte)5, JobGroup.Healer)]
    [InlineData((byte)2, 30u, (byte)1, JobGroup.Melee)]
    [InlineData((byte)2, 30u, (byte)2, JobGroup.Melee)]
    [InlineData((byte)3, 30u, (byte)2, JobGroup.Ranged)]
    [InlineData((byte)3, 31u, (byte)4, JobGroup.Magic)]
    [InlineData((byte)0, 33u, (byte)0, JobGroup.Crafter)]
    [InlineData((byte)0, 32u, (byte)0, JobGroup.Gatherer)]
    public void GroupOf_KnownJobs(byte role, uint category, byte stat, JobGroup expected)
        => Assert.Equal(expected, JobGroups.GroupOf(role, category, stat));

    [Fact]
    public void BaseClassesShareGroupWithJob()
    {
        Assert.Equal(JobGroup.Tank, JobGroups.GroupOf(1, 30, 1));               // GLA, like PLD
        Assert.Equal(JobGroup.Healer, JobGroups.GroupOf(4, 31, 5));             // CNJ
        Assert.Equal(JobGroup.Ranged, JobGroups.GroupOf(3, 30, 2));             // ARC
        Assert.Equal(JobGroup.Magic, JobGroups.GroupOf(3, 31, 4));              // THM, ACN
    }

    [Fact]
    public void DisplayNamesAreGerman()
        => Assert.Equal(["Tank", "Heiler", "Nahkampf", "Fernkampf", "Magie", "Handwerk", "Sammeln"],
            Enum.GetValues<JobGroup>().Select(JobGroups.DisplayName));
}
