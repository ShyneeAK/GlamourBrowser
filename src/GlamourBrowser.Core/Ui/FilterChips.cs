using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Ui;

/// <param name="WithoutThis">The filter state with only this chip's filter reset.</param>
public sealed record FilterChip(string Label, FilterState WithoutThis);

/// <summary>One removable chip per active filter, in a fixed order.</summary>
public static class FilterChips
{
    public static IReadOnlyList<FilterChip> From(FilterState state, Func<EquipSlot, string> slotName, Func<uint, string> jobName)
    {
        var defaults = new FilterState();
        var chips = new List<FilterChip>();

        if (state.SearchText.Trim().Length > 0)
            chips.Add(new FilterChip($"Suche: {state.SearchText.Trim()}", state with { SearchText = "" }));

        if (state.Sets)
            chips.Add(new FilterChip("Serien", state with { Sets = false }));

        foreach (var slot in Enum.GetValues<EquipSlot>().Where(state.Slots.Contains))
            chips.Add(new FilterChip(slotName(slot), state with { Slots = state.Slots.Where(s => s != slot).ToHashSet() }));

        if (state.ClassJobId is { } job)
            chips.Add(new FilterChip(jobName(job), state with { ClassJobId = null }));

        if (state.MinLevel != defaults.MinLevel || state.MaxLevel != defaults.MaxLevel)
            chips.Add(new FilterChip($"Level {state.MinLevel}–{state.MaxLevel}", state with { MinLevel = defaults.MinLevel, MaxLevel = defaults.MaxLevel }));

        foreach (var count in state.DyeCounts.Order())
            chips.Add(new FilterChip(DyeLabel(count), state with { DyeCounts = state.DyeCounts.Where(c => c != count).ToHashSet() }));

        if (state.Ownership != OwnershipFilter.All)
            chips.Add(new FilterChip(state.Ownership == OwnershipFilter.Owned ? "Habe ich" : "Fehlt mir", state with { Ownership = OwnershipFilter.All }));

        if (!state.WearableOnly)
            chips.Add(new FilterChip("Auch nicht tragbare", state with { WearableOnly = true }));

        foreach (var category in Enum.GetValues<SourceCategory>().Where(state.Categories.Contains))
            chips.Add(new FilterChip(SourceCategories.DisplayName(category), state with { Categories = state.Categories.Where(c => c != category).ToHashSet() }));

        foreach (var expansion in Enum.GetValues<Expansion>().Where(state.Expansions.Contains))
            chips.Add(new FilterChip(Expansions.DisplayName(expansion), state with { Expansions = state.Expansions.Where(e => e != expansion).ToHashSet() }));

        return chips;
    }

    private static string DyeLabel(int count) => count switch
    {
        0 => "nicht färbbar",
        1 => "1 Kanal",
        _ => $"{count} Kanäle",
    };
}
