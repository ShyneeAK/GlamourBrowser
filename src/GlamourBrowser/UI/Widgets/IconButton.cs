using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A square button showing a game icon (or a FontAwesome glyph when <c>gameIcon</c> is 0), tooltip on hover.</summary>
public static class IconButton
{
    /// <param name="size">Edge length; defaults to the frame height.</param>
    public static bool Draw(string id, uint gameIcon, FontAwesomeIcon fallback, string tooltip, Theme.Theme theme, float? size = null)
    {
        var edge = new Vector2(size ?? ImGui.GetFrameHeight());
        var colors = theme.Colors;
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, edge);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, min + edge, hovered ? colors.CardRaised : colors.Card, 5f);
        if (hovered)
            drawList.AddRect(min, min + edge, colors.Border, 5f, ImDrawFlags.None, 1f);
        var pad = new Vector2(edge.X * 0.15f);
        GameIcon.DrawAt(drawList, gameIcon, min + pad, edge - pad * 2, fallback, hovered ? colors.Text : colors.TextMuted);

        if (hovered && tooltip.Length > 0)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
