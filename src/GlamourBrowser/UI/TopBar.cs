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
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        // The filter-column toggle comes first so it can never be pushed out of a narrow window.
        if (ImGuiComponents.IconButton("##filtercolumn", FontAwesomeIcon.Filter))
        {
            config.FilterColumnVisible = !config.FilterColumnVisible;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(config.FilterColumnVisible ? "Filterspalte ausblenden" : "Filterspalte einblenden");

        // Follow external changes (e.g. a removed search chip) unless the user is typing.
        if (!searchActive)
            search = state.Filter.SearchText;
        NextOnLine(180 * scale);
        ImGui.SetNextItemWidth(180 * scale);
        if (ImGui.InputTextWithHint("##search", "Suchen …", ref search, 100))
            state.Filter = state.Filter with { SearchText = search };
        searchActive = ImGui.IsItemActive();

        var jobLabel = state.Filter.ClassJobId is { } id ? jobs.Find(id)?.Abbreviation ?? "Job" : "Job";
        NextOnLine(ImGui.CalcTextSize(jobLabel).X + ImGui.GetStyle().FramePadding.X * 2);
        PopupButton.Draw("jobpicker", jobLabel, () =>
        {
            var selected = state.Filter.ClassJobId;
            if (JobPicker.Draw("jobs", jobs, ref selected, theme))
                state.Filter = state.Filter with { ClassJobId = selected };
        });

        NextOnLine(130 * scale);
        ImGui.SetNextItemWidth(130 * scale);
        var mode = (int)config.TileMode;
        if (ImGui.Combo("##mode", ref mode, ["Aktuelles Outfit", "Unterwäsche"], 2))
            state.SetTileMode((TileMode)mode);

        // Drag fields instead of sliders: no grab knob covering the label.
        var columns = config.Columns;
        var rows = config.Rows;
        NextOnLine(160 * scale + spacing);
        ImGui.SetNextItemWidth(80 * scale);
        var gridChanged = ImGui.DragInt("##columns", ref columns, 0.05f, 2, 10, "%d Spalten", ImGuiSliderFlags.AlwaysClamp);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * scale);
        gridChanged |= ImGui.DragInt("##rows", ref rows, 0.05f, 1, 6, "%d Zeilen", ImGuiSliderFlags.AlwaysClamp);
        if (gridChanged)
            state.SetGrid(columns, rows);

        // Paging and hits stay together as one unit.
        var pageText = $"Seite {state.PageIndex + 1} / {state.PageCount}";
        var hitsText = $"{state.Results.Count} Treffer";
        var arrow = ImGui.GetFrameHeight();
        NextOnLine(arrow * 2 + ImGui.CalcTextSize(pageText).X + ImGui.CalcTextSize(hitsText).X + spacing * 3);
        if (ImGui.ArrowButton("##prev", ImGuiDir.Left))
            state.PreviousPage();
        ImGui.SameLine();
        ImGui.TextUnformatted(pageText);
        ImGui.SameLine();
        if (ImGui.ArrowButton("##next", ImGuiDir.Right))
            state.NextPage();
        ImGui.SameLine();
        ImGui.TextDisabled(hitsText);

        DrawChips();
    }

    /// <summary>Continues on the current line when <paramref name="width"/> still fits, otherwise starts a new one.</summary>
    private static void NextOnLine(float width)
    {
        ImGui.SameLine();
        if (ImGui.GetContentRegionAvail().X < width)
            ImGui.NewLine();
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

        NextOnLine(ImGui.CalcTextSize("Alle zurücksetzen").X + ImGui.GetStyle().FramePadding.X * 2);
        if (ImGui.SmallButton("Alle zurücksetzen"))
            state.Filter = new FilterState();
    }

    private static string SlotName(EquipSlot slot) => SlotNames.All.FirstOrDefault(s => s.Slot == slot).Name ?? slot.ToString();

    private string JobName(uint id) => jobs.Find(id)?.Abbreviation ?? id.ToString();
}
