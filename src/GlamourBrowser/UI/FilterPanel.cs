using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Ownership;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.UI;

/// <summary>Left column: filters and the list of known containers.</summary>
public sealed class FilterPanel
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly (uint Id, string Label)[] jobs;
    private string search = "";

    public FilterPanel(BrowserState state, OwnershipService ownership)
    {
        this.state = state;
        this.ownership = ownership;
        jobs = Plugin.DataManager.GetExcelSheet<ClassJob>()
            .Where(j => j.RowId != 0 && !j.Abbreviation.IsEmpty)
            .OrderBy(j => j.UIPriority)
            .Select(j => (j.RowId, $"{j.Abbreviation.ExtractText()} – {j.Name.ExtractText()}"))
            .ToArray();
    }

    public void Draw()
    {
        var f = state.Filter;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##search", "Suchen …", ref search, 100))
            state.Filter = f with { SearchText = search };

        ImGui.Spacing();
        ImGui.TextDisabled("Slot");
        foreach (var (slot, name) in SlotNames.All)
        {
            var on = f.Slots.Contains(slot);
            if (ImGui.Checkbox(name, ref on))
                state.Filter = f with { Slots = Toggle(f.Slots, slot, on) };
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Job");
        ImGui.SetNextItemWidth(-1);
        var current = f.ClassJobId is { } id ? jobs.FirstOrDefault(j => j.Id == id).Label ?? "Alle" : "Alle";
        if (ImGui.BeginCombo("##job", current))
        {
            if (ImGui.Selectable("Alle", f.ClassJobId == null))
                state.Filter = f with { ClassJobId = null };
            foreach (var (jobId, label) in jobs)
            {
                if (ImGui.Selectable(label, f.ClassJobId == jobId))
                    state.Filter = f with { ClassJobId = jobId };
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Level");
        var min = f.MinLevel;
        var max = f.MaxLevel;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##minlevel", ref min, 1, 100, "von %d"))
            state.Filter = f with { MinLevel = Math.Min(min, f.MaxLevel) };
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##maxlevel", ref max, 1, 100, "bis %d"))
            state.Filter = f with { MaxLevel = Math.Max(max, f.MinLevel) };

        ImGui.Spacing();
        ImGui.TextDisabled("Färbbar");
        foreach (var (count, label) in new[] { (0, "nicht färbbar"), (1, "1 Kanal"), (2, "2 Kanäle") })
        {
            var on = f.DyeCounts.Contains(count);
            if (ImGui.Checkbox(label, ref on))
                state.Filter = f with { DyeCounts = Toggle(f.DyeCounts, count, on) };
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Besitz");
        foreach (var (value, label) in new[] { (OwnershipFilter.All, "Alle"), (OwnershipFilter.Owned, "Habe ich"), (OwnershipFilter.Missing, "Fehlt mir") })
        {
            if (ImGui.RadioButton(label, f.Ownership == value))
                state.Filter = f with { Ownership = value };
        }

        var wearable = f.WearableOnly;
        if (ImGui.Checkbox("Nur für mich tragbar", ref wearable))
            state.Filter = f with { WearableOnly = wearable };

        ImGui.Separator();
        DrawContainers();
    }

    private void DrawContainers()
    {
        ImGui.TextDisabled("Erfasste Behälter");
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
