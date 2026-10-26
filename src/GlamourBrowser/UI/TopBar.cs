using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Search, tile mode, sort, grid size, hit count, filter-column toggle and the active filter chips (jobs and tile dye sit under the slot bar, paging below the grid).</summary>
public sealed class TopBar
{
    private readonly BrowserState state;
    private readonly Configuration config;
    private readonly Theme.Theme theme;
    private readonly DyeCatalog dyes;
    private readonly TileFilterControls filterControls;
    private string search = "";
    private bool searchActive;

    public TopBar(BrowserState state, Configuration config, Theme.Theme theme, DyeCatalog dyes, TileFilterControls filterControls)
    {
        this.dyes = dyes;
        this.state = state;
        this.config = config;
        this.theme = theme;
        this.filterControls = filterControls;
    }

    public void Draw()
    {
        var scale = UiScale.Global;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        // The filter-column toggle comes first so it can never be pushed out of a narrow window.
        if (IconButton.Draw("##filtercolumn", 0, FontAwesomeIcon.Filter, "", theme))
        {
            config.FilterColumnVisible = !config.FilterColumnVisible;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(config.FilterColumnVisible ? Loc.T("Filterspalte ausblenden") : Loc.T("Filterspalte einblenden"));

        // Follow external changes (e.g. a removed search chip) unless the user is typing.
        if (!searchActive)
            search = state.Filter.SearchText;
        NextOnLine(180 * scale);
        ImGui.SetNextItemWidth(180 * scale);
        if (ImGui.InputTextWithHint("##search", Loc.T("Suchen …"), ref search, 100))
            state.Filter = state.Filter with { SearchText = search };
        searchActive = ImGui.IsItemActive();

        // Sort does not apply to the wishlist and the custom sets (newest first).
        var onlySearch = BrowseModes.OnlySearch(state.Filter.Mode);

        NextOnLine(130 * scale);
        ImGui.SetNextItemWidth(130 * scale);
        var mode = (int)config.TileMode;
        if (ImGui.Combo("##mode", ref mode, [Loc.T("Aktuelles Outfit"), Loc.T("Unterwäsche")], 2))
            state.SetTileMode((TileMode)mode);

        NextOnLine(130 * scale);
        ImGui.SetNextItemWidth(130 * scale);
        var sort = (int)config.Sort;
        using (ImRaii.Disabled(onlySearch))
        {
            if (ImGui.Combo("##sort", ref sort, [Loc.T("Nach Level"), Loc.T("Neueste zuerst")], 2))
                state.SetSort((SortOrder)sort);
        }
        DisabledHint(onlySearch);

        // A picker instead of drag fields: its grid shows the size and takes a new one with one click.
        var columns = config.Columns;
        var rows = config.Rows;
        NextOnLine(ImGui.CalcTextSize("10 × 6").X + ImGui.GetStyle().FramePadding.X * 2);
        if (GridPicker.Draw("gridsize", ref columns, ref rows, theme))
            state.SetGrid(columns, rows);

        var hitsText = Loc.F("{0} Treffer", state.Results.Count);
        NextOnLine(ImGui.CalcTextSize(hitsText).X);
        ImGui.AlignTextToFramePadding();
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
        var chips = FilterChips.From(state.Filter, SlotName, filterControls.JobParts, dyes.NameOf);
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

        var resetAll = Loc.T("Alle zurücksetzen");
        NextOnLine(ImGui.CalcTextSize(resetAll).X + ImGui.GetStyle().FramePadding.X * 2);
        if (ImGui.SmallButton(resetAll))
            state.Filter = new FilterState();
    }

    private static string SlotName(EquipSlot slot) => SlotNames.All.FirstOrDefault(s => s.Slot == slot).Name ?? slot.ToString();

    /// <summary>Why the control before it is grayed out in the wishlist and custom set modes.</summary>
    private void DisabledHint(bool onlySearch)
    {
        if (onlySearch && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(BrowseModes.NotApplicable(state.Filter.Mode));
    }
}
