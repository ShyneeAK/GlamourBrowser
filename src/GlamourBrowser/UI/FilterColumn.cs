using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Rendering;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Rendering;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Ownership;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Filters next to the grid: collapsible sections for level, ownership, sources, expansions and the captured containers, plus the dye switch.</summary>
public sealed class FilterColumn
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly Configuration config;
    private readonly SourceService sources;
    private readonly Theme.Theme theme;

    private static readonly Vector4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    private readonly WristSides wristSides;

    public FilterColumn(BrowserState state, OwnershipService ownership, Configuration config, SourceService sources, Theme.Theme theme, WristSides wristSides)
    {
        this.wristSides = wristSides;
        this.state = state;
        this.ownership = ownership;
        this.config = config;
        this.sources = sources;
        this.theme = theme;
    }

    public void Draw()
    {
        var f = state.Filter;

        // Above the sections, so it shows with all of them collapsed: source and expansion filters then miss items.
        if (sources.IsReady && sources.Index.HasFailures)
        {
            ImGui.TextColored(WarningColor, Loc.T("Einige Quellen konnten nicht geladen werden"));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Loc.F("Fehlt: {0}", string.Join(", ", sources.Index.Failures.Select(Loc.T))));
        }

        // The wishlist and custom set modes only follow the search; the containers stay readable.
        var onlySearch = BrowseModes.OnlySearch(f.Mode);
        if (onlySearch)
            ImGui.TextDisabled(BrowseModes.NotApplicable(f.Mode));
        using (ImRaii.Disabled(onlySearch))
            DrawFilterSections(f);

        if (Section.Begin("containers", Loc.T("Behälter"), config))
        {
            DrawContainers();
            Section.End();
        }
    }

    private void DrawFilterSections(FilterState f)
    {
        if (Section.Begin("level", Loc.T("Level"), config))
        {
            var min = f.MinLevel;
            var max = f.MaxLevel;
            ImGui.TextDisabled($"{min} – {max}");
            if (RangeSlider.Draw("##level", ref min, ref max, 1, 100, theme))
                state.Filter = f with { MinLevel = min, MaxLevel = max };
            Section.End();
        }

        // One switch instead of a section: whether an item can be dyed at all is what matters.
        var dyeable = f.DyeableOnly;
        using (ImRaii.Disabled(f.Mode == BrowseMode.Sets))
        {
            if (ImGui.Checkbox(Loc.T("Nur Färbbare"), ref dyeable))
                state.Filter = f with { DyeableOnly = dyeable };
        }
        if (f.Mode == BrowseMode.Sets && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("gilt nur für einzelne Items"));
        ImGui.Spacing();

        // Only with the bracelets shown: many are on one arm, and the sides combine.
        if (f.Mode == BrowseMode.Items && f.Slots.Contains(EquipSlot.Wrists) && Section.Begin("wrists", Loc.T("Armband-Seite"), config))
        {
            if (!wristSides.IsReady)
                ImGui.TextDisabled(Loc.T("Armbänder werden gelesen …"));
            using (ImRaii.Disabled(!wristSides.IsReady))
            {
                foreach (var side in Enum.GetValues<WristSide>())
                {
                    var on = f.Wrists.Contains(side);
                    if (ImGui.Checkbox(FilterChips.WristSideName(side), ref on))
                        state.Filter = f with { Wrists = Toggle(f.Wrists, side, on) };
                }
            }
            Section.End();
        }

        if (Section.Begin("ownership", Loc.T("Besitz"), config))
        {
            foreach (var (value, label) in new[] { (OwnershipFilter.All, Loc.T("Alle")), (OwnershipFilter.Owned, Loc.T("Habe ich")), (OwnershipFilter.Missing, Loc.T("Fehlt mir")) })
            {
                if (ImGui.RadioButton(label, f.Ownership == value))
                    state.Filter = f with { Ownership = value };
            }
            var wearable = f.WearableOnly;
            if (ImGui.Checkbox(Loc.T("Nur für mich tragbar"), ref wearable))
                state.Filter = f with { WearableOnly = wearable };
            Section.End();
        }

        if (Section.Begin("sources", Loc.T("Quelle"), config))
        {
            DrawSources(f);
            Section.End();
        }

        if (Section.Begin("expansions", Loc.T("Erweiterung"), config))
        {
            // The patch of an item comes from the source index; until it is built the filter would do nothing.
            if (!sources.IsReady)
                ImGui.TextDisabled(Loc.T("Quellen werden geladen …"));
            using (ImRaii.Disabled(!sources.IsReady))
            {
                foreach (var expansion in Enum.GetValues<Expansion>())
                {
                    var on = f.Expansions.Contains(expansion);
                    if (ImGui.Checkbox(Expansions.LongName(expansion), ref on))
                        state.Filter = f with { Expansions = Toggle(f.Expansions, expansion, on) };
                }
            }
            Section.End();
        }
    }

    private void DrawContainers()
    {
        if (ownership.Current is not { } store)
        {
            ImGui.TextUnformatted(Loc.T("Nicht eingeloggt"));
            return;
        }

        foreach (var c in store.Containers.OrderBy(c => c.Id.Kind).ThenBy(c => c.DisplayName))
            ImGui.TextUnformatted($"{c.DisplayName} · {c.Seen:dd.MM. HH:mm}");

        var known = store.Containers.Select(c => c.Id).ToHashSet();
        foreach (var name in NeverOpened(known))
            ImGui.TextDisabled(Loc.F("{0}: noch nie geöffnet", name));
    }

    private static unsafe IEnumerable<string> NeverOpened(HashSet<ContainerId> known)
    {
        var result = new List<string>();
        if (!known.Contains(new ContainerId(ContainerKind.Dresser, "")))
            result.Add(Loc.T("Kommode"));
        if (!known.Contains(new ContainerId(ContainerKind.Armoire, "")))
            result.Add(Loc.T("Armoire"));

        var retainers = RetainerManager.Instance();
        if (retainers != null && retainers->IsReady)
        {
            foreach (var r in retainers->Retainers)
            {
                if (r.RetainerId != 0 && !known.Contains(new ContainerId(ContainerKind.Retainer, r.RetainerId.ToString())))
                    result.Add(Loc.F("Gehilfe {0}", r.NameString));
            }
        }
        return result;
    }

    /// <summary>
    /// The width the column needs for its longest row (a source category with its count, an expansion), so nothing is
    /// cut off with a larger text size or a longer translation.
    /// </summary>
    public float NeededWidth()
    {
        var style = ImGui.GetStyle();
        var counts = state.CountsWithoutCategories;
        var checkbox = ImGui.GetFrameHeight() + style.ItemInnerSpacing.X;
        var categories = Enum.GetValues<SourceCategory>()
            .Select(c => ImGui.CalcTextSize($"{SourceCategories.DisplayName(c)} ({counts.GetValueOrDefault(c)})").X)
            .Max() + ImGui.GetFrameHeight() * 2 + style.ItemSpacing.X * 2;
        var expansions = Enum.GetValues<Expansion>().Select(e => ImGui.CalcTextSize(Expansions.LongName(e)).X).Max() + checkbox;
        // The section indent, the child's padding and border, and the scrollbar.
        return MathF.Max(categories, expansions) + 6 + style.WindowPadding.X * 2 + style.ScrollbarSize + 2;
    }

    private void DrawSources(FilterState f)
    {
        if (!sources.IsReady)
            ImGui.TextDisabled(Loc.T("Quellen werden geladen …"));

        using var disabled = ImRaii.Disabled(!sources.IsReady);
        var counts = state.CountsWithoutCategories;
        // The icons as large as the checkboxes, the text centred on the row.
        var iconSize = new Vector2(ImGui.GetFrameHeight());
        foreach (var category in Enum.GetValues<SourceCategory>())
        {
            var on = f.Categories.Contains(category);
            var changed = ImGui.Checkbox($"##cat{category}", ref on);
            ImGui.SameLine();
            GameIcon.Draw(SourceIcons.IconFor(category), iconSize, SourceIcons.Fallback(category), theme.Colors.TextMuted);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{SourceCategories.DisplayName(category)} ({counts.GetValueOrDefault(category)})");
            if (changed)
                state.Filter = f with { Categories = Toggle(f.Categories, category, on) };
        }
    }

    private static IReadOnlySet<T> Toggle<T>(IReadOnlySet<T> set, T value, bool on)
    {
        var copy = new HashSet<T>(set);
        if (on)
            copy.Add(value);
        else
            copy.Remove(value);
        return copy;
    }
}
