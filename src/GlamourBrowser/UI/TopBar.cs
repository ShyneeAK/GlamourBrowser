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

/// <summary>Search, tile mode, sort, grid size, hit count and the active filter chips (jobs and tile dye sit under the slot bar, paging below the grid).</summary>
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

        // Follow external changes (e.g. a removed search chip) unless the user is typing.
        if (!searchActive)
            search = state.Filter.SearchText;
        ImGui.SetNextItemWidth(180 * scale);
        if (ImGui.InputTextWithHint("##search", Loc.T("Suchen …"), ref search, 100))
            state.Filter = state.Filter with { SearchText = search };
        searchActive = ImGui.IsItemActive();

        // Sort does not apply to the wishlist and the custom sets (newest first).
        var onlySearch = BrowseModes.OnlySearch(state.Filter.Mode);

        // The tile mode (current outfit or underwear) is the "Alles anlegen" button of the live view, as in the game's try-on.
        var sort = Array.IndexOf(SortOrders, config.Sort);
        using (ImRaii.Disabled(onlySearch))
        {
            if (LabeledCombo("##sort", Loc.T("Sortierung"), 150 * scale, ref sort, SortOrders.Select(SortName).ToArray()) && sort >= 0)
                state.SetSort(SortOrders[sort]);
        }
        DisabledHint(onlySearch);

        // A picker instead of drag fields: its grid shows the size and takes a new one with one click.
        var columns = config.Columns;
        var rows = config.Rows;
        var gridLabel = Loc.T("Kacheln pro Seite");
        NextOnLine(ImGui.CalcTextSize(gridLabel).X + spacing + ImGui.CalcTextSize("10 × 6").X + ImGui.GetStyle().FramePadding.X * 2);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(gridLabel);
        ImGui.SameLine();
        if (GridPicker.Draw("gridsize", ref columns, ref rows, theme))
            state.SetGrid(columns, rows);

        var hitsText = Loc.F("{0} Treffer", state.Results.Count);
        NextOnLine(ImGui.CalcTextSize(hitsText).X);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(hitsText);

        DrawChips();
    }

    // The order in the sort combo (the enum keeps the order the values were added in).
    private static readonly SortOrder[] SortOrders =
        [SortOrder.Level, SortOrder.LevelAscending, SortOrder.Newest, SortOrder.Oldest, SortOrder.NameAscending, SortOrder.NameDescending];

    private static string SortName(SortOrder sort) => Loc.T(sort switch
    {
        SortOrder.LevelAscending => "Level aufsteigend",
        SortOrder.Newest => "Neueste zuerst",
        SortOrder.Oldest => "Älteste zuerst",
        SortOrder.NameAscending => "Name A–Z",
        SortOrder.NameDescending => "Name Z–A",
        _ => "Level absteigend",
    });

    /// <summary>A combo with its label in front, kept on one line (both wrap together).</summary>
    private static bool LabeledCombo(string id, string label, float width, ref int index, string[] items)
    {
        NextOnLine(ImGui.CalcTextSize(label).X + ImGui.GetStyle().ItemSpacing.X + width);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width);
        return ImGui.Combo(id, ref index, items, items.Length);
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

        // The mode stays: its own chip leaves it.
        if (!FilterChips.CanResetAll(state.Filter))
            return;
        var resetAll = Loc.T("Alle zurücksetzen");
        NextOnLine(ImGui.CalcTextSize(resetAll).X + ImGui.GetStyle().FramePadding.X * 2);
        if (ImGui.SmallButton(resetAll))
            state.Filter = FilterChips.ResetAll(state.Filter);
    }

    private static string SlotName(EquipSlot slot) => SlotNames.All.FirstOrDefault(s => s.Slot == slot).Name ?? slot.ToString();

    /// <summary>Why the control before it is grayed out in the wishlist and custom set modes.</summary>
    private void DisabledHint(bool onlySearch)
    {
        if (onlySearch && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(BrowseModes.NotApplicable(state.Filter.Mode));
    }
}
