using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// Job icons grouped by role: a click chooses or drops a job, a click on a group's name chooses the whole group,
/// "Alle Jobs" clears; "Nur job-exklusive" switches from gear any chosen job can wear to gear made for them alone.
/// Meant to be drawn inside a popup, which stays open for more clicks.
/// </summary>
public static class JobPicker
{
    private const int PerRow = 8;

    /// <param name="groups">Every job (and class) of <paramref name="jobs"/> with its group.</param>
    public static bool Draw(string id, JobCatalog jobs, IReadOnlyList<(uint Id, JobGroup Group)> groups, ref IReadOnlySet<uint> selected,
                            ref bool exclusive, Theme.Theme theme)
    {
        var changed = false;
        if (ImGui.Selectable(Loc.T("Alle Jobs"), selected.Count == 0, ImGuiSelectableFlags.DontClosePopups))
        {
            selected = new HashSet<uint>();
            changed = true;
        }
        changed |= ImGui.Checkbox(Loc.T("Nur job-exklusive") + $"##{id}-exclusive", ref exclusive);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(exclusive
                ? Loc.T("An: nur Ausrüstung, die für die gewählten Jobs gemacht ist (keine, die auch andere Jobs tragen)")
                : Loc.T("Aus: alles, was einer der gewählten Jobs tragen kann"));

        var size = new Vector2(32, 32) * UiScale.Global;
        foreach (var group in jobs.Pickable.GroupBy(j => j.Group))
        {
            ImGui.Spacing();
            var name = JobGroups.DisplayName(group.Key);
            var current = selected;
            var whole = group.All(j => current.Contains(j.Id));
            // As wide as the name: it reads as a heading that can be clicked.
            if (ImGui.Selectable($"{name}##{id}-group-{group.Key}", whole, ImGuiSelectableFlags.DontClosePopups, new Vector2(ImGui.CalcTextSize(name).X, 0)))
            {
                selected = JobSelection.ToggleGroup(selected, groups, group.Key);
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(whole ? Loc.F("{0} abwählen", name) : Loc.F("Alle Jobs von {0} wählen", name));

            var index = 0;
            foreach (var job in group)
            {
                if (index++ % PerRow != 0)
                    ImGui.SameLine();
                if (IconToggle.Draw($"{id}-{job.Id}", job.IconId, size, selected.Contains(job.Id), $"{job.Abbreviation} – {job.Name}", theme, FontAwesomeIcon.User))
                {
                    selected = JobSelection.Toggle(selected, job.Id);
                    changed = true;
                }
            }
        }
        return changed;
    }
}
