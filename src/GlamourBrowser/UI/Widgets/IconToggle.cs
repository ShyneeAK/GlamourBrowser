using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A game icon used as a toggle button: accent frame when active, tooltip on hover.</summary>
public static class IconToggle
{
    public static bool Draw(string id, uint iconId, Vector2 size, bool active, string tooltip, Theme.Theme theme, FontAwesomeIcon fallback)
    {
        var colors = theme.Colors;
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, min + size, hovered ? colors.CardRaised : colors.Card, 6f);
        var pad = new Vector2(3, 3);
        GameIcon.DrawAt(drawList, iconId, min + pad, size - pad * 2, fallback, colors.TextMuted);
        if (active)
            drawList.AddRect(min, min + size, colors.Accent, 6f, ImDrawFlags.None, 2f);
        else if (hovered)
            drawList.AddRect(min, min + size, colors.Border, 6f, ImDrawFlags.None, 1f);

        if (hovered && tooltip.Length > 0)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
