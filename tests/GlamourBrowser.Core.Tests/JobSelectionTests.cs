using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class JobSelectionTests
{
    // Paladin 19, Warrior 21, Gladiator 1 (tank class); Carpenter 8, Blacksmith 9 (crafters); Miner 16 (gatherer).
    private static readonly IReadOnlyList<(uint Id, JobGroup Group)> Jobs =
        [(19, JobGroup.Tank), (21, JobGroup.Tank), (1, JobGroup.Tank), (8, JobGroup.Crafter), (9, JobGroup.Crafter), (16, JobGroup.Gatherer)];

    private static string Name(uint id) => id switch { 19 => "PLD", 21 => "WAR", 1 => "GLA", 8 => "CRP", 9 => "BSM", 16 => "MIN", _ => $"#{id}" };

    private static HashSet<uint> Set(params uint[] ids) => [.. ids];

    [Fact]
    public void Toggle_AddsAndRemovesOneJob()
    {
        var both = JobSelection.Toggle(Set(19), 21);
        Assert.Equal(Set(19, 21), both);
        Assert.Equal(Set(21), JobSelection.Toggle(both, 19));
    }

    [Fact]
    public void ToggleGroup_SelectsTheWholeGroup_AndClearsItWhenComplete()
    {
        var crafters = JobSelection.ToggleGroup(Set(19, 8), Jobs, JobGroup.Crafter);
        Assert.Equal(Set(19, 8, 9), crafters);
        Assert.Equal(Set(19), JobSelection.ToggleGroup(crafters, Jobs, JobGroup.Crafter));
    }

    [Fact]
    public void Parts_NameWholeGroups_AndSingleJobs()
    {
        var parts = JobSelection.Parts(Set(8, 9, 19, 16), Jobs, Name);
        Assert.Equal(["PLD", "Handwerk", "MIN"], parts.Select(p => p.Label));
        Assert.Equal(Set(8, 9), parts[1].Ids);
    }

    [Fact]
    public void Parts_UnknownIdsKeepTheirName()
        => Assert.Equal(["#99"], JobSelection.Parts(Set(99), Jobs, Name).Select(p => p.Label));

    [Theory]
    [InlineData(new uint[0], "Job")]
    [InlineData(new uint[] { 19 }, "PLD")]
    [InlineData(new uint[] { 8, 9 }, "Handwerk")]
    [InlineData(new uint[] { 19, 21, 16 }, "PLD +2")]
    public void Label_ForTheButton(uint[] selected, string expected)
        => Assert.Equal(expected, JobSelection.Label(JobSelection.Parts(Set(selected), Jobs, Name)));
}
