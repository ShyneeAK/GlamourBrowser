using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A game icon used as a toggle button: accent frame when active, tooltip on hover.</summary>
public static class IconToggle
{
    /// <param name="enabled">False draws it faded and ignores clicks; the tooltip still shows (to say why).</param>
    public static bool Draw(string id, uint iconId, Vector2 size, bool active, string tooltip, Theme.Theme theme, FontAwesomeIcon fallback, bool enabled = true)
    {
        var colors = theme.Colors;
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size) && enabled;
        var hovered = ImGui.IsItemHovered();
        var highlight = hovered && enabled;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, min + size, highlight ? colors.CardRaised : colors.Card, 6f);
        var pad = new Vector2(3, 3);
        // Faded: the glyph at half its alpha.
        GameIcon.DrawAt(drawList, iconId, min + pad, size - pad * 2, fallback, enabled ? colors.TextMuted : (colors.TextMuted & 0x00FFFFFF) | 0x60000000);
        if (active)
            drawList.AddRect(min, min + size, colors.Accent, 6f, ImDrawFlags.None, 2f);
        else if (highlight)
            drawList.AddRect(min, min + size, colors.Border, 6f, ImDrawFlags.None, 1f);

        if (hovered && tooltip.Length > 0)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
