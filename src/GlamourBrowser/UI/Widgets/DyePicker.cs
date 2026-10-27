using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// A dye field: the colour, empty for none, crossed out where the item has no such channel; a click opens all dyes by
/// shade, plus "Keine Farbe".
/// </summary>
public static class DyePicker
{
    private const int PerRow = 12;

    /// <param name="enabled">False when the item has no such dye channel: crossed out, no popup.</param>
    /// <param name="label">Put before the dye's name in the tooltip (the tile dye: "Farbe").</param>
    /// <param name="edge">The field's size; the frame height without one.</param>
    public static bool Draw(string id, ref byte stain, DyeCatalog dyes, Theme.Theme theme, bool enabled, string? label = null, float? edge = null)
    {
        var colors = theme.Colors;
        var size = new Vector2(edge ?? ImGui.GetFrameHeight());
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size) && enabled;
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        var pad = new Vector2(3);
        if (!enabled)
        {
            drawList.AddLine(min + pad, min + size - pad, colors.Border, 1.5f);
            drawList.AddLine(min + new Vector2(size.X - pad.X, pad.Y), min + new Vector2(pad.X, size.Y - pad.Y), colors.Border, 1.5f);
        }
        else if (stain != 0 && dyes.Find(stain) is { } current)
            drawList.AddRectFilled(min + pad, min + size - pad, current.Color, 4f);
        drawList.AddRect(min + pad, min + size - pad, hovered && enabled ? colors.Text : colors.Border, 4f);
        if (hovered)
            ImGui.SetTooltip(!enabled ? Loc.T("nicht färbbar") : label != null ? $"{label}: {dyes.NameOf(stain)}" : dyes.NameOf(stain));

        var popup = $"{id}-dyes";
        if (clicked)
            ImGui.OpenPopup(popup);
        if (!ImGui.BeginPopup(popup))
            return false;
        var changed = Palette(ref stain, dyes, theme);
        ImGui.EndPopup();
        return changed;
    }

    /// <summary>The content of a dye popup: "Keine Farbe" and every dye by shade; a pick closes the popup.</summary>
    public static bool Palette(ref byte stain, DyeCatalog dyes, Theme.Theme theme)
    {
        var colors = theme.Colors;
        var changed = false;
        if (ImGui.Selectable(Loc.T("Keine Farbe"), stain == 0))
        {
            stain = 0;
            changed = true;
        }
        var swatch = new Vector2(ImGui.GetFrameHeight());
        foreach (var shade in dyes.All.GroupBy(d => d.Shade))
        {
            // Room between the colour groups, so they read as groups.
            ImGui.Dummy(new Vector2(0, 6 * UiScale.Global));
            var index = 0;
            foreach (var dye in shade)
            {
                if (index++ % PerRow != 0)
                    ImGui.SameLine(0, 2);
                var at = ImGui.GetCursorScreenPos();
                if (ImGui.InvisibleButton($"##dye{dye.Id}", swatch))
                {
                    stain = dye.Id;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }
                var popupList = ImGui.GetWindowDrawList();
                popupList.AddRectFilled(at, at + swatch, dye.Color, 3f);
                if (dye.Id == stain || ImGui.IsItemHovered())
                    popupList.AddRect(at, at + swatch, colors.Text, 3f, ImDrawFlags.None, 2f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(dye.Name);
            }
        }
        return changed;
    }
}
