using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// A button "6 × 3" that opens a grid of cells like a table picker: the cells up to the mouse light up, a click takes
/// that many columns × rows.
/// </summary>
public static class GridPicker
{
    private const float Gap = 3f;

    public static bool Draw(string id, ref int columns, ref int rows, Theme.Theme theme)
    {
        if (ImGui.Button($"{columns} × {rows}##{id}"))
            ImGui.OpenPopup(id);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Kacheln pro Seite (Spalten × Zeilen)"));
        if (!ImGui.BeginPopup(id))
            return false;

        var colors = theme.Colors;
        var cell = MathF.Round(ImGui.GetFrameHeight() * 0.9f);
        var step = cell + Gap;
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("##cells", new Vector2(PageLayout.MaxColumns * step - Gap, PageLayout.MaxRows * step - Gap));
        var mouse = ImGui.GetIO().MousePos - origin;
        var hovered = ImGui.IsItemHovered() ? GridPick.CellAt(mouse.X, mouse.Y, step, PageLayout.MaxColumns, PageLayout.MaxRows) : null;
        // Fewer than two columns are not possible; the highlight shows what a click gives.
        var (showColumns, showRows) = hovered is { } at ? PageLayout.ClampGrid(at.Columns, at.Rows) : (columns, rows);

        var drawList = ImGui.GetWindowDrawList();
        for (var row = 0; row < PageLayout.MaxRows; row++)
        {
            for (var column = 0; column < PageLayout.MaxColumns; column++)
            {
                var min = origin + new Vector2(column * step, row * step);
                var on = column < showColumns && row < showRows;
                drawList.AddRectFilled(min, min + new Vector2(cell), on ? colors.Accent : colors.Card, 3f);
                drawList.AddRect(min, min + new Vector2(cell), colors.Border, 3f);
            }
        }
        ImGui.TextUnformatted(Loc.F("{0} Spalten × {1} Zeilen", showColumns, showRows));

        var changed = false;
        if (clicked && hovered != null)
        {
            (columns, rows) = (showColumns, showRows);
            changed = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        return changed;
    }
}
