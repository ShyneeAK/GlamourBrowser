using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Ui;

/// <param name="WithoutThis">The filter state with only this chip's filter reset.</param>
public sealed record FilterChip(string Label, FilterState WithoutThis);

/// <summary>One removable chip per active filter, in a fixed order; in the wishlist and custom set modes only the filters that apply there.</summary>
public static class FilterChips
{
    /// <param name="jobParts">The job selection as whole groups and single jobs, see <see cref="JobSelection.Parts"/>.</param>
    /// <param name="dyeName">The tile dye's name for its chip; without it the chip shows the stain id.</param>
    public static IReadOnlyList<FilterChip> From(FilterState state, Func<EquipSlot, string> slotName, Func<IReadOnlySet<uint>, IReadOnlyList<JobPart>> jobParts,
                                                 Func<byte, string>? dyeName = null)
    {
        var defaults = new FilterState();
        var chips = new List<FilterChip>();

        if (state.SearchText.Trim().Length > 0)
            chips.Add(new FilterChip($"Suche: {state.SearchText.Trim()}", state with { SearchText = "" }));

        if (state.Mode != BrowseMode.Items)
            chips.Add(new FilterChip(BrowseModes.DisplayName(state.Mode), state with { Mode = BrowseMode.Items }));
        // The other filters keep their state for when the mode ends, but do not apply to the wishlist or the custom sets.
        if (BrowseModes.OnlySearch(state.Mode))
            return chips;

        foreach (var slot in Enum.GetValues<EquipSlot>().Where(state.Slots.Contains))
            chips.Add(new FilterChip(slotName(slot), state with { Slots = state.Slots.Where(s => s != slot).ToHashSet() }));

        if (state.Jobs.Count > 0)
        {
            foreach (var part in jobParts(state.Jobs))
                chips.Add(new FilterChip(part.Label, state with { Jobs = state.Jobs.Where(j => !part.Ids.Contains(j)).ToHashSet() }));
        }

        if (state.MinLevel != defaults.MinLevel || state.MaxLevel != defaults.MaxLevel)
            chips.Add(new FilterChip($"Level {state.MinLevel}–{state.MaxLevel}", state with { MinLevel = defaults.MinLevel, MaxLevel = defaults.MaxLevel }));

        if (state.DyeableOnly)
            chips.Add(new FilterChip("Nur Färbbare", state with { DyeableOnly = false }));

        string Name(byte id) => dyeName?.Invoke(id) ?? id.ToString();
        if (state.Dye.First != 0)
            chips.Add(new FilterChip($"Farbe 1: {Name(state.Dye.First)}", state with { Dye = state.Dye with { First = 0 } }));
        if (state.Dye.Second != 0)
            chips.Add(new FilterChip($"Farbe 2: {Name(state.Dye.Second)}", state with { Dye = state.Dye with { Second = 0 } }));

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
}
