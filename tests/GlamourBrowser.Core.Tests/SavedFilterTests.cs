using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class SavedFilterTests
{
    private static readonly FilterState Full = new()
    {
        SearchText = "robe",
        Mode = BrowseMode.Sets,
        Slots = new HashSet<EquipSlot> { EquipSlot.Body, EquipSlot.Feet },
        Jobs = new HashSet<uint> { 19, 21 },
        JobsExclusive = true,
        MinLevel = 50,
        MaxLevel = 90,
        DyeableOnly = true,
        Wrists = new HashSet<WristSide> { WristSide.Left },
        Dye = new DyePair(81, 82),
        Ownership = OwnershipFilter.Missing,
        WearableOnly = false,
        Categories = new HashSet<SourceCategory> { SourceCategory.Crafting },
        Expansions = new HashSet<Expansion> { Expansion.Heavensward },
    };

    [Fact]
    public void RoundTrip_KeepsEverythingButTheSearch()
    {
        var back = SavedFilter.From(Full).ToState();

        Assert.True(SavedFilter.SameKept(Full, back));
        Assert.Equal("", back.SearchText);
    }

    [Fact]
    public void SameKept_IgnoresTheSearchButNotTheRest()
    {
        Assert.True(SavedFilter.SameKept(Full, Full with { SearchText = "" }));
        Assert.False(SavedFilter.SameKept(Full, Full with { Slots = new HashSet<EquipSlot> { EquipSlot.Body } }));
        Assert.False(SavedFilter.SameKept(Full, Full with { Dye = default }));
    }

    [Fact]
    public void ToState_LeavesOutUnknownValuesAndClampsLevels()
    {
        var saved = new SavedFilter
        {
            Mode = (BrowseMode)99,
            Slots = [EquipSlot.Head, (EquipSlot)99],
            MinLevel = 120,
            MaxLevel = 5,
            Ownership = (OwnershipFilter)7,
            Categories = null!,
        };

        var state = saved.ToState();

        Assert.Equal(BrowseMode.Items, state.Mode);
        Assert.Equal([EquipSlot.Head], state.Slots);
        Assert.Equal((100, 100), (state.MinLevel, state.MaxLevel));
        Assert.Equal(OwnershipFilter.All, state.Ownership);
        Assert.Empty(state.Categories);
    }
}
