using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class FilterChipsTests
{
    private static string SlotName(EquipSlot s) => s switch { EquipSlot.Head => "Kopf", EquipSlot.Feet => "Füße", _ => s.ToString() };
    private static readonly IReadOnlyList<(uint Id, JobGroup Group)> Jobs = [(19, JobGroup.Tank), (21, JobGroup.Tank), (8, JobGroup.Crafter), (9, JobGroup.Crafter)];
    private static string JobName(uint id) => id switch { 19 => "PLD", 21 => "WAR", 8 => "CRP", 9 => "BSM", _ => "?" };
    private static IReadOnlyList<FilterChip> Chips(FilterState s) => FilterChips.From(s, SlotName, jobs => JobSelection.Parts(jobs, Jobs, JobName));

    [Fact]
    public void NoActiveFilters_NoChips() => Assert.Empty(Chips(new FilterState()));

    [Fact]
    public void Dye_GivesAChipPerChannelThatRemovesOnlyIt()
    {
        var state = new FilterState { Dye = new DyePair(81, 82) };
        var chips = FilterChips.From(state, SlotName, jobs => JobSelection.Parts(jobs, Jobs, JobName), id => id == 81 ? "Schwarz" : "Weiß");

        Assert.Equal(["Farbe 1: Schwarz", "Farbe 2: Weiß"], chips.Select(c => c.Label));
        Assert.Equal(new DyePair(0, 82), chips[0].WithoutThis.Dye);
        Assert.Equal(new DyePair(81, 0), chips[1].WithoutThis.Dye);
    }

    [Fact]
    public void ResetAll_KeepsTheModeAndClearsItsFilters()
    {
        var state = new FilterState { Mode = BrowseMode.Sets, SearchText = "robe", Ownership = OwnershipFilter.Missing, Dye = new DyePair(81, 0) };

        var reset = FilterChips.ResetAll(state);

        Assert.Equal(BrowseMode.Sets, reset.Mode);
        Assert.Equal("", reset.SearchText);
        Assert.Equal(OwnershipFilter.All, reset.Ownership);
        Assert.Equal(default, reset.Dye);
    }

    [Fact]
    public void ResetAll_OnlyTheSearchInTheWishlistMode()
    {
        var jobs = new HashSet<uint> { 19 };
        var reset = FilterChips.ResetAll(new FilterState { Mode = BrowseMode.Wishlist, SearchText = "robe", Jobs = jobs });

        Assert.Equal(BrowseMode.Wishlist, reset.Mode);
        Assert.Equal("", reset.SearchText);
        Assert.Same(jobs, reset.Jobs);
    }

    [Fact]
    public void CanResetAll_NotForTheModeChipAlone()
    {
        Assert.False(FilterChips.CanResetAll(new FilterState { Mode = BrowseMode.MySets, Jobs = new HashSet<uint> { 19 } }));
        Assert.True(FilterChips.CanResetAll(new FilterState { Mode = BrowseMode.MySets, SearchText = "a" }));
        Assert.False(FilterChips.CanResetAll(new FilterState { Mode = BrowseMode.Sets }));
        Assert.True(FilterChips.CanResetAll(new FilterState { Mode = BrowseMode.Sets, DyeableOnly = true }));
    }

    [Fact]
    public void Dye_NoChipInTheWishlistMode()
        => Assert.DoesNotContain(Chips(new FilterState { Dye = new DyePair(81, 0), Mode = BrowseMode.Wishlist }), c => c.Label.StartsWith("Farbe"));

    [Fact]
    public void OneChipPerActiveFilter()
    {
        var state = new FilterState
        {
            SearchText = "robe",
            Slots = new HashSet<EquipSlot> { EquipSlot.Head, EquipSlot.Feet },
            Jobs = new HashSet<uint> { 19 },
            MinLevel = 50,
            MaxLevel = 70,
            DyeableOnly = true,
            Ownership = OwnershipFilter.Owned,
            WearableOnly = false,
        };

        Assert.Equal(["Suche: robe", "Kopf", "Füße", "PLD", "Level 50–70", "Nur Färbbare", "Habe ich", "Auch nicht tragbare"],
            Chips(state).Select(c => c.Label));
    }

    [Fact]
    public void ExclusiveJobs_HaveAChipOnlyWithJobs()
    {
        var chips = Chips(new FilterState { Jobs = new HashSet<uint> { 19 }, JobsExclusive = true });
        Assert.Equal(["PLD", "Nur job-exklusive"], chips.Select(c => c.Label));
        Assert.False(chips[1].WithoutThis.JobsExclusive);
        Assert.Empty(Chips(new FilterState { JobsExclusive = true }));
    }

    [Fact]
    public void DuplicatesChip_ShowsTheDuplicatesAgain()
    {
        var chip = Assert.Single(Chips(new FilterState { HideDuplicates = true }));
        Assert.Equal("Modell-Duplikate ausblenden", chip.Label);
        Assert.False(chip.WithoutThis.HideDuplicates);
        // Sets have no duplicates.
        Assert.DoesNotContain(Chips(new FilterState { HideDuplicates = true, Mode = BrowseMode.Sets }), c => c.Label == chip.Label);
    }

    [Fact]
    public void DyeChip_RemovesTheDyeFilter()
        => Assert.False(Assert.Single(Chips(new FilterState { DyeableOnly = true })).WithoutThis.DyeableOnly);

    [Fact]
    public void Chips_RemoveSlot()
    {
        var state = new FilterState { Slots = new HashSet<EquipSlot> { EquipSlot.Head, EquipSlot.Feet }, Jobs = new HashSet<uint> { 19 } };
        var without = Chips(state).Single(c => c.Label == "Kopf").WithoutThis;

        Assert.Equal(new HashSet<EquipSlot> { EquipSlot.Feet }, without.Slots);
        Assert.Equal(new HashSet<uint> { 19 }, without.Jobs);
    }

    [Fact]
    public void JobChips_OnePerGroupOrJob_AndRemoveOnlyTheirJobs()
    {
        var chips = Chips(new FilterState { Jobs = new HashSet<uint> { 19, 8, 9 } });
        Assert.Equal(["PLD", "Handwerk"], chips.Select(c => c.Label));
        Assert.Equal(new HashSet<uint> { 19 }, chips[1].WithoutThis.Jobs);
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

    [Theory]
    [InlineData(BrowseMode.Sets, "Ausrüstungs-Sets")]
    [InlineData(BrowseMode.MySets, "Meine Sets")]
    [InlineData(BrowseMode.Wishlist, "Wunschliste")]
    public void ModeChip_EndsTheMode(BrowseMode mode, string label)
    {
        var chip = Assert.Single(Chips(new FilterState { Mode = mode }));
        Assert.Equal(label, chip.Label);
        Assert.Equal(BrowseMode.Items, chip.WithoutThis.Mode);
    }

    [Fact]
    public void WishlistMode_ShowsOnlySearchAndWishlistChips()
    {
        // The other filters keep their state for later, but do not apply to the wishlist.
        var state = new FilterState { Mode = BrowseMode.Wishlist, SearchText = "robe", Jobs = new HashSet<uint> { 19 }, MinLevel = 50, Ownership = OwnershipFilter.Owned };
        Assert.Equal(["Suche: robe", "Wunschliste"], Chips(state).Select(c => c.Label));
    }

    [Fact]
    public void MySetsChip_OnlySearchAndMode()
        => Assert.Equal(["Suche: robe", "Meine Sets"],
            Chips(new FilterState { Mode = BrowseMode.MySets, SearchText = "robe", DyeableOnly = true, Jobs = new HashSet<uint> { 19 } }).Select(c => c.Label));
}
