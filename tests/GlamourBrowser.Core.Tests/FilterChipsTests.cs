using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class FilterChipsTests
{
    private static string SlotName(EquipSlot s) => s switch { EquipSlot.Head => "Kopf", EquipSlot.Feet => "Füße", _ => s.ToString() };
    private static string JobName(uint id) => id == 19 ? "PLD" : "?";
    private static IReadOnlyList<FilterChip> Chips(FilterState s) => FilterChips.From(s, SlotName, JobName);

    [Fact]
    public void NoActiveFilters_NoChips() => Assert.Empty(Chips(new FilterState()));

    [Fact]
    public void OneChipPerActiveFilter()
    {
        var state = new FilterState
        {
            SearchText = "robe",
            Slots = new HashSet<EquipSlot> { EquipSlot.Head, EquipSlot.Feet },
            ClassJobId = 19,
            MinLevel = 50,
            MaxLevel = 70,
            DyeCounts = new HashSet<int> { 2 },
            Ownership = OwnershipFilter.Owned,
            WearableOnly = false,
        };

        Assert.Equal(["Suche: robe", "Kopf", "Füße", "PLD", "Level 50–70", "2 Kanäle", "Habe ich", "Auch nicht tragbare"],
            Chips(state).Select(c => c.Label));
    }

    [Fact]
    public void Chips_RemoveSlot()
    {
        var state = new FilterState { Slots = new HashSet<EquipSlot> { EquipSlot.Head, EquipSlot.Feet }, ClassJobId = 19 };
        var without = Chips(state).Single(c => c.Label == "Kopf").WithoutThis;

        Assert.Equal(new HashSet<EquipSlot> { EquipSlot.Feet }, without.Slots);
        Assert.Equal(19u, without.ClassJobId);
    }

    [Fact]
    public void RemovingLevelResetsBothBounds()
    {
        var without = Chips(new FilterState { MinLevel = 50, MaxLevel = 70 }).Single().WithoutThis;
        Assert.Equal(1, without.MinLevel);
        Assert.Equal(100, without.MaxLevel);
    }

    [Fact]
    public void WearableChipOnlyWhenOff() => Assert.Empty(Chips(new FilterState { WearableOnly = true }));

    [Fact]
    public void CategoryAndExpansionChips()
    {
        var state = new FilterState
        {
            Categories = new HashSet<SourceCategory> { SourceCategory.RaidSavage },
            Expansions = new HashSet<Expansion> { Expansion.Dawntrail },
        };
        var chips = Chips(state);

        Assert.Equal(["Raid (Episch)", "DT"], chips.Select(c => c.Label));
        var without = chips[0].WithoutThis;
        Assert.Empty(without.Categories);
        Assert.Equal(new HashSet<Expansion> { Expansion.Dawntrail }, without.Expansions);
    }

    [Fact]
    public void SetsChip_RemovesSetMode()
    {
        var chip = Assert.Single(Chips(new FilterState { Sets = true }));
        Assert.Equal("Serien", chip.Label);
        Assert.False(chip.WithoutThis.Sets);
    }
}
