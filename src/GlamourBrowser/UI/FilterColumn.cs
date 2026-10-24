using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Ownership;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Collapsible filter sections next to the grid: level, dyes, ownership and the captured containers.</summary>
public sealed class FilterColumn
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly Configuration config;

    public FilterColumn(BrowserState state, OwnershipService ownership, Configuration config)
    {
        this.state = state;
        this.ownership = ownership;
        this.config = config;
    }

    public void Draw()
    {
        var f = state.Filter;

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
            foreach (var (count, label) in new[] { (0, "nicht färbbar"), (1, "1 Kanal"), (2, "2 Kanäle") })
            {
                var on = f.DyeCounts.Contains(count);
                if (ImGui.Checkbox(label, ref on))
                    state.Filter = f with { DyeCounts = Toggle(f.DyeCounts, count, on) };
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
