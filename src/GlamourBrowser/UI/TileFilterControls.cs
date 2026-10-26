using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>
/// The job popup and the tile dye, in the second row under the slot bar; both are grayed out in the wishlist and the
/// custom set modes, where they do not apply.
/// </summary>
public sealed class TileFilterControls
{
    private readonly BrowserState state;
    private readonly JobCatalog jobs;
    private readonly DyeCatalog dyes;
    private readonly Theme.Theme theme;
    // Every job the filter offers with its group, for choosing whole groups and naming the choice.
    private readonly IReadOnlyList<(uint Id, JobGroup Group)> jobGroups;

    public TileFilterControls(BrowserState state, JobCatalog jobs, DyeCatalog dyes, Theme.Theme theme)
    {
        this.state = state;
        this.jobs = jobs;
        this.dyes = dyes;
        this.theme = theme;
        jobGroups = jobs.Pickable.Select(j => (j.Id, j.Group)).ToList();
    }

    /// <summary>The chosen jobs as named parts (whole groups by their name), for the button and the filter chips.</summary>
    public IReadOnlyList<JobPart> JobParts(IReadOnlySet<uint> selected) => JobSelection.Parts(selected, jobGroups, JobName);

    /// <summary>
    /// The tile dye, one picker per channel, as large as the mode icons next to it (<paramref name="edge"/>): like a
    /// filter (each has a chip), but it colours the dyeable tiles instead of hiding any.
    /// </summary>
    public void DrawDye(float edge)
    {
        var onlySearch = BrowseModes.OnlySearch(state.Filter.Mode);
        var dye = state.Filter.Dye;
        var first = dye.First;
        var second = dye.Second;
        using (ImRaii.Disabled(onlySearch))
        {
            DrawLargeLabel(Loc.T("Farbe"), edge);
            ImGui.SameLine();
            if (DyePicker.Draw("##tiledye1", ref first, dyes, theme, enabled: true, label: Loc.T("Farbe 1"), edge))
                state.Filter = state.Filter with { Dye = dye with { First = first } };
            DisabledHint(onlySearch);
            ImGui.SameLine();
            if (DyePicker.Draw("##tiledye2", ref second, dyes, theme, enabled: true, label: Loc.T("Farbe 2"), edge))
                state.Filter = state.Filter with { Dye = dye with { Second = second } };
        }
        DisabledHint(onlySearch);
    }

    /// <summary>The job button, as high as the mode icons, in the larger font.</summary>
    public void DrawJobs(float edge)
    {
        var onlySearch = BrowseModes.OnlySearch(state.Filter.Mode);
        using (ImRaii.Disabled(onlySearch))
        {
            bool open;
            using (UiScale.PushLarge())
            {
                var label = JobSelection.Label(JobParts(state.Filter.Jobs));
                var width = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 4;
                open = ImGui.Button($"{label}##jobpicker", new Vector2(width, edge));
            }
            if (open)
                ImGui.OpenPopup("jobpicker");
            if (ImGui.BeginPopup("jobpicker"))
            {
                var selected = state.Filter.Jobs;
                var exclusive = state.Filter.JobsExclusive;
                if (JobPicker.Draw("jobs", jobs, jobGroups, ref selected, ref exclusive, theme))
                    state.Filter = state.Filter with { Jobs = selected, JobsExclusive = exclusive };
                ImGui.EndPopup();
            }
        }
        DisabledHint(onlySearch);
    }

    /// <summary>The width of the job button, to tell whether it still fits on the row.</summary>
    public float JobsWidth()
    {
        using (UiScale.PushLarge())
            return ImGui.CalcTextSize(JobSelection.Label(JobParts(state.Filter.Jobs))).X + ImGui.GetStyle().FramePadding.X * 4;
    }

    /// <summary>
    /// A label in the larger font, centred on a row <paramref name="height"/> high. Drawn by hand: after the taller mode
    /// icons ImGui would put a text lower.
    /// </summary>
    private static void DrawLargeLabel(string text, float height)
    {
        using (UiScale.PushLarge())
        {
            var top = ImGui.GetCursorScreenPos();
            var size = ImGui.CalcTextSize(text);
            ImGui.Dummy(size with { Y = height });
            ImGui.GetWindowDrawList().AddText(top + new Vector2(0, (height - size.Y) / 2), ImGui.GetColorU32(ImGuiCol.Text), text);
        }
    }

    private string JobName(uint id) => jobs.Find(id)?.Abbreviation ?? id.ToString();

    /// <summary>Why the control before it is grayed out in the wishlist and custom set modes.</summary>
    private void DisabledHint(bool onlySearch)
    {
        if (onlySearch && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(BrowseModes.NotApplicable(state.Filter.Mode));
    }
}
