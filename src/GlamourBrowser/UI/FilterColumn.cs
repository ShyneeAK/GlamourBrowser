using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Ownership;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Collapsible filter sections next to the grid: level, dyes, ownership and the captured containers.</summary>
public sealed class FilterColumn
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly Configuration config;
    private readonly SourceService sources;
    private readonly Theme.Theme theme;

    private static readonly Vector4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    public FilterColumn(BrowserState state, OwnershipService ownership, Configuration config, SourceService sources, Theme.Theme theme)
    {
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
            ImGui.TextColored(WarningColor, "Einige Quellen konnten nicht geladen werden");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Fehlt: " + string.Join(", ", sources.Index.Failures));
        }

        if (Section.Begin("level", "Level", config))
        {
            var min = f.MinLevel;
            var max = f.MaxLevel;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragIntRange2("##level", ref min, ref max, 0.2f, 1, 100, "von %d", "bis %d"))
                state.Filter = f with { MinLevel = Math.Clamp(min, 1, 100), MaxLevel = Math.Clamp(Math.Max(max, min), 1, 100) };
            Section.End();
        }

        if (Section.Begin("dye", "Färbbar", config))
        {
            if (f.Sets)
                ImGui.TextDisabled("gilt nur für einzelne Items");
            using (ImRaii.Disabled(f.Sets))
            {
                foreach (var (count, label) in new[] { (0, "nicht färbbar"), (1, "1 Kanal"), (2, "2 Kanäle") })
                {
                    var on = f.DyeCounts.Contains(count);
                    if (ImGui.Checkbox(label, ref on))
                        state.Filter = f with { DyeCounts = Toggle(f.DyeCounts, count, on) };
                }
            }
            Section.End();
        }

        if (Section.Begin("ownership", "Besitz", config))
        {
            foreach (var (value, label) in new[] { (OwnershipFilter.All, "Alle"), (OwnershipFilter.Owned, "Habe ich"), (OwnershipFilter.Missing, "Fehlt mir") })
            {
                if (ImGui.RadioButton(label, f.Ownership == value))
                    state.Filter = f with { Ownership = value };
            }
            var wearable = f.WearableOnly;
            if (ImGui.Checkbox("Nur für mich tragbar", ref wearable))
                state.Filter = f with { WearableOnly = wearable };
            Section.End();
        }

        if (Section.Begin("sources", "Quelle", config))
        {
            DrawSources(f);
            Section.End();
        }

        if (Section.Begin("expansions", "Erweiterung", config))
        {
            // The patch of an item comes from the source index; until it is built the filter would do nothing.
            if (!sources.IsReady)
                ImGui.TextDisabled("Quellen werden geladen …");
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

        if (Section.Begin("containers", "Behälter", config))
        {
            DrawContainers();
            Section.End();
        }
    }

    private void DrawContainers()
    {
        if (ownership.Current is not { } store)
        {
            ImGui.TextUnformatted("Nicht eingeloggt");
            return;
        }

        foreach (var c in store.Containers.OrderBy(c => c.Id.Kind).ThenBy(c => c.DisplayName))
            ImGui.TextUnformatted($"{c.DisplayName} · {c.Seen:dd.MM. HH:mm}");

        var known = store.Containers.Select(c => c.Id).ToHashSet();
        foreach (var name in NeverOpened(known))
            ImGui.TextDisabled($"{name}: noch nie geöffnet");
    }

    private static unsafe IEnumerable<string> NeverOpened(HashSet<ContainerId> known)
    {
        var result = new List<string>();
        if (!known.Contains(new ContainerId(ContainerKind.Dresser, "")))
            result.Add("Kommode");
        if (!known.Contains(new ContainerId(ContainerKind.Armoire, "")))
            result.Add("Armoire");

        var retainers = RetainerManager.Instance();
        if (retainers != null && retainers->IsReady)
        {
            foreach (var r in retainers->Retainers)
            {
                if (r.RetainerId != 0 && !known.Contains(new ContainerId(ContainerKind.Retainer, r.RetainerId.ToString())))
                    result.Add($"Gehilfe {r.NameString}");
            }
        }
        return result;
    }

    private void DrawSources(FilterState f)
    {
        if (!sources.IsReady)
            ImGui.TextDisabled("Quellen werden geladen …");

        using var disabled = ImRaii.Disabled(!sources.IsReady);
        var counts = state.CountsWithoutCategories;
        var iconSize = new Vector2(ImGui.GetTextLineHeight());
        foreach (var category in Enum.GetValues<SourceCategory>())
        {
            var on = f.Categories.Contains(category);
            var changed = ImGui.Checkbox($"##cat{category}", ref on);
            ImGui.SameLine();
            GameIcon.Draw(SourceIcons.IconFor(category), iconSize, SourceIcons.Fallback(category), theme.Colors.TextMuted);
            ImGui.SameLine();
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
