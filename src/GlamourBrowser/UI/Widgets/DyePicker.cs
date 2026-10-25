using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A dye field: the colour (or a slash for none); a click opens all dyes by shade, plus "Keine Farbe".</summary>
public static class DyePicker
{
    private const int PerRow = 12;

    /// <param name="enabled">False when the item has no such dye channel: faded, no popup.</param>
    public static bool Draw(string id, ref byte stain, DyeCatalog dyes, Theme.Theme theme, bool enabled)
    {
        var colors = theme.Colors;
        var size = new Vector2(ImGui.GetFrameHeight());
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size) && enabled;
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        var pad = new Vector2(3);
        if (stain != 0 && dyes.Find(stain) is { } current)
            drawList.AddRectFilled(min + pad, min + size - pad, enabled ? current.Color : (current.Color & 0x00FFFFFF) | 0x60000000, 4f);
        else
            drawList.AddLine(min + pad + new Vector2(0, size.Y - pad.Y * 2), min + new Vector2(size.X - pad.X, pad.Y), enabled ? colors.TextMuted : colors.Border, 1.5f);
        drawList.AddRect(min + pad, min + size - pad, hovered && enabled ? colors.Text : colors.Border, 4f);
        if (hovered)
            ImGui.SetTooltip(enabled ? dyes.NameOf(stain) : "nicht färbbar");

        var popup = $"{id}-dyes";
        if (clicked)
            ImGui.OpenPopup(popup);
        if (!ImGui.BeginPopup(popup))
            return false;

        var changed = false;
        if (ImGui.Selectable("Keine Farbe", stain == 0))
        {
            stain = 0;
            changed = true;
        }
        var swatch = new Vector2(ImGui.GetFrameHeight());
        foreach (var shade in dyes.All.GroupBy(d => d.Shade))
        {
            ImGui.Spacing();
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
        ImGui.EndPopup();
        return changed;
    }
}
