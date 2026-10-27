using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// One of the game's round window buttons (see <see cref="TryonIcons"/>), drawn as the game draws it: slightly dimmed at
/// rest, full on hover, the gold ring around it while active, faded when disabled.
/// </summary>
public static class RoundButton
{
    /// <param name="enabled">False draws it faded and ignores clicks; the tooltip still shows (to say why).</param>
    public static bool Draw(string id, UiIcon icon, float edge, bool active, string tooltip, Theme.Theme theme, FontAwesomeIcon fallback, bool enabled = true)
    {
        var size = new Vector2(edge);
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size) && enabled;
        var hovered = ImGui.IsItemHovered();
        // Inside ImRaii.Disabled (style alpha lowered) it fades like the other controls.
        var faded = !enabled || ImGui.GetStyle().Alpha < 1f;

        var drawList = ImGui.GetWindowDrawList();
        var tint = faded ? 0x60FFFFFFu : hovered ? 0xFFFFFFFFu : 0xFFD0D0D0u;
        GameIcon.DrawAt(drawList, icon, min, size, fallback, theme.Colors.TextMuted, tint);
        if (active)
        {
            // The ring is 28 px around a 24 px button.
            var ring = size * (28f / 24f);
            GameIcon.DrawAt(drawList, TryonIcons.ActiveRing, min - (ring - size) / 2, ring, FontAwesomeIcon.Circle, theme.Colors.Accent,
                            faded ? 0x60FFFFFFu : 0xFFFFFFFFu);
        }

        if (hovered && tooltip.Length > 0)
            ImGui.SetTooltip(tooltip);
        return clicked;
    }
}
