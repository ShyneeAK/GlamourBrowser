using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Search, job popup, tile mode, grid size, paging, filter-column toggle and the active filter chips.</summary>
public sealed class TopBar
{
    private readonly BrowserState state;
    private readonly JobCatalog jobs;
    private readonly Configuration config;
    private readonly Theme.Theme theme;
    private string search = "";
    private bool searchActive;

    public TopBar(BrowserState state, JobCatalog jobs, Configuration config, Theme.Theme theme)
    {
        this.state = state;
        this.jobs = jobs;
        this.config = config;
        this.theme = theme;
    }

    public void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;

        // Follow external changes (e.g. a removed search chip) unless the user is typing.
        if (!searchActive)
            search = state.Filter.SearchText;
        ImGui.SetNextItemWidth(180 * scale);
        if (ImGui.InputTextWithHint("##search", "Suchen …", ref search, 100))
            state.Filter = state.Filter with { SearchText = search };
        searchActive = ImGui.IsItemActive();

        ImGui.SameLine();
        var jobLabel = state.Filter.ClassJobId is { } id ? jobs.Find(id)?.Abbreviation ?? "Job" : "Job";
        PopupButton.Draw("jobpicker", jobLabel, () =>
        {
            var selected = state.Filter.ClassJobId;
            if (JobPicker.Draw("jobs", jobs, ref selected, theme))
                state.Filter = state.Filter with { ClassJobId = selected };
        });

        ImGui.SameLine();
        ImGui.SetNextItemWidth(130 * scale);
        var mode = (int)config.TileMode;
        if (ImGui.Combo("##mode", ref mode, ["Aktuelles Outfit", "Unterwäsche"], 2))
            state.SetTileMode((TileMode)mode);

        ImGui.SameLine();
        var columns = config.Columns;
        var rows = config.Rows;
        ImGui.SetNextItemWidth(80 * scale);
        var gridChanged = ImGui.SliderInt("##columns", ref columns, 2, 10, "%d Spalten");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * scale);
        gridChanged |= ImGui.SliderInt("##rows", ref rows, 1, 6, "%d Zeilen");
        if (gridChanged)
            state.SetGrid(columns, rows);

        ImGui.SameLine();
        if (ImGui.ArrowButton("##prev", ImGuiDir.Left))
            state.PreviousPage();
        ImGui.SameLine();
        ImGui.TextUnformatted($"{state.PageIndex + 1} / {state.PageCount}");
        ImGui.SameLine();
        if (ImGui.ArrowButton("##next", ImGuiDir.Right))
            state.NextPage();
        ImGui.SameLine();
        ImGui.TextDisabled($"{state.Results.Count} Treffer");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##filtercolumn", FontAwesomeIcon.Filter))
        {
            config.FilterColumnVisible = !config.FilterColumnVisible;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(config.FilterColumnVisible ? "Filterspalte ausblenden" : "Filterspalte einblenden");

        DrawChips();
    }

    private void DrawChips()
    {
        var chips = FilterChips.From(state.Filter, SlotName, JobName);
        if (chips.Count == 0)
            return;

        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        for (var i = 0; i < chips.Count; i++)
        {
            // Wrap to a new line when the next chip would not fit.
            var width = ImGui.CalcTextSize(chips[i].Label).X + 20 + ImGui.GetTextLineHeight() + 6;
            if (i > 0)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + width > right)
                    ImGui.NewLine();
            }
            if (Chip.Draw($"chip{i}", chips[i].Label, theme, closable: true) == ChipResult.Removed)
                state.Filter = chips[i].WithoutThis;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Alle zurücksetzen"))
            state.Filter = new FilterState();
    }

    private static string SlotName(EquipSlot slot) => SlotNames.All.FirstOrDefault(s => s.Slot == slot).Name ?? slot.ToString();

    private string JobName(uint id) => jobs.Find(id)?.Abbreviation ?? id.ToString();
}
