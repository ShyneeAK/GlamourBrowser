using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Rendering;
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
            chips.Add(new FilterChip(Loc.F("Suche: {0}", state.SearchText.Trim()), state with { SearchText = "" }));

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
            if (state.JobsExclusive)
                chips.Add(new FilterChip(Loc.T("Nur job-exklusive"), state with { JobsExclusive = false }));
        }

        if (state.MinLevel != defaults.MinLevel || state.MaxLevel != defaults.MaxLevel)
            chips.Add(new FilterChip(Loc.F("Level {0}–{1}", state.MinLevel, state.MaxLevel), state with { MinLevel = defaults.MinLevel, MaxLevel = defaults.MaxLevel }));

        if (state.DyeableOnly)
            chips.Add(new FilterChip(Loc.T("Nur Färbbare"), state with { DyeableOnly = false }));

        foreach (var wrist in Enum.GetValues<WristSide>().Where(state.Wrists.Contains))
            chips.Add(new FilterChip(WristSideName(wrist), state with { Wrists = state.Wrists.Where(w => w != wrist).ToHashSet() }));

        string Name(byte id) => dyeName?.Invoke(id) ?? id.ToString();
        if (state.Dye.First != 0)
            chips.Add(new FilterChip(Loc.F("Farbe 1: {0}", Name(state.Dye.First)), state with { Dye = state.Dye with { First = 0 } }));
        if (state.Dye.Second != 0)
            chips.Add(new FilterChip(Loc.F("Farbe 2: {0}", Name(state.Dye.Second)), state with { Dye = state.Dye with { Second = 0 } }));

        if (state.Ownership != OwnershipFilter.All)
            chips.Add(new FilterChip(Loc.T(state.Ownership == OwnershipFilter.Owned ? "Habe ich" : "Fehlt mir"), state with { Ownership = OwnershipFilter.All }));

        if (state.HideDuplicates)
            chips.Add(new FilterChip(DuplicatesName, state with { HideDuplicates = false }));

        if (!state.WearableOnly)
            chips.Add(new FilterChip(Loc.T("Auch nicht tragbare"), state with { WearableOnly = true }));

        foreach (var category in Enum.GetValues<SourceCategory>().Where(state.Categories.Contains))
            chips.Add(new FilterChip(SourceCategories.DisplayName(category), state with { Categories = state.Categories.Where(c => c != category).ToHashSet() }));

        foreach (var expansion in Enum.GetValues<Expansion>().Where(state.Expansions.Contains))
            chips.Add(new FilterChip(Expansions.DisplayName(expansion), state with { Expansions = state.Expansions.Where(e => e != expansion).ToHashSet() }));

        return chips;
    }

    /// <summary>
    /// "Alle zurücksetzen": every filter with a chip back to its default, in the mode that is open. In the wishlist and
    /// custom set modes only the search, like their chips: the other filters wait unseen for the items.
    /// </summary>
    public static FilterState ResetAll(FilterState state)
        => BrowseModes.OnlySearch(state.Mode) ? state with { SearchText = "" } : new FilterState { Mode = state.Mode };

    /// <summary>Whether <see cref="ResetAll"/> would change anything besides the mode chip.</summary>
    public static bool CanResetAll(FilterState state)
        => BrowseModes.OnlySearch(state.Mode)
            ? state.SearchText.Trim().Length > 0
            : state.Jobs.Count > 0 || From(state with { Mode = BrowseMode.Items }, _ => "", _ => []).Count > 0;

    /// <summary>The checkbox and the chip of <see cref="FilterState.HideDuplicates"/>.</summary>
    public static string DuplicatesName => Loc.T("Modell-Duplikate ausblenden");

    /// <summary>The name of a bracelet side, for its checkbox and its chip.</summary>
    public static string WristSideName(WristSide side) => side switch
    {
        WristSide.Right => Loc.T("Armband nur rechts"),
        WristSide.Left => Loc.T("Armband nur links"),
        _ => Loc.T("Armband an beiden Armen"),
    };
}
