using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>Job icons grouped by role; one job or none ("Alle"). Meant to be drawn inside a popup.</summary>
public static class JobPicker
{
    private const int PerRow = 8;

    public static bool Draw(string id, JobCatalog jobs, ref uint? selected, Theme.Theme theme)
    {
        var changed = false;
        if (ImGui.Selectable("Alle Jobs", selected == null))
        {
            selected = null;
            changed = true;
        }

        var size = new Vector2(32, 32) * UiScale.Global;
        foreach (var group in jobs.All.GroupBy(j => j.Group))
        {
            ImGui.Spacing();
            ImGui.TextDisabled(JobGroups.DisplayName(group.Key));
            var index = 0;
            foreach (var job in group)
            {
                if (index++ % PerRow != 0)
                    ImGui.SameLine();
                if (IconToggle.Draw($"{id}-{job.Id}", job.IconId, size, selected == job.Id, $"{job.Abbreviation} – {job.Name}", theme, FontAwesomeIcon.User))
                {
                    selected = selected == job.Id ? null : job.Id;
                    changed = true;
                }
            }
        }

        if (changed)
            ImGui.CloseCurrentPopup();
        return changed;
    }
}
