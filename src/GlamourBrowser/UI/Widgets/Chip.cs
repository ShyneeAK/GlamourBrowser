using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace GlamourBrowser.UI.Widgets;

public enum ChipResult
{
    None,
    Clicked,
    Removed,
}

/// <summary>Pill-shaped label on the muted accent, optionally with a remove cross.</summary>
public static class Chip
{
    public static ChipResult Draw(string id, string label, Theme.Theme theme, bool closable)
    {
        var colors = theme.Colors;
        var textSize = ImGui.CalcTextSize(label);
        string cross;
        using (UiScale.PushIcons())
            cross = FontAwesomeIcon.Times.ToIconString();
        var crossWidth = closable ? textSize.Y + 6 : 0;
        var size = new Vector2(textSize.X + 20 + crossWidth, textSize.Y + 8);

        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, min + size, hovered ? colors.Accent : colors.AccentMuted, size.Y / 2);
        drawList.AddText(min + new Vector2(10, 4), colors.Text, label);
        if (closable)
        {
            using (UiScale.PushIcons())
                drawList.AddText(min + new Vector2(size.X - crossWidth - 4, 4), colors.Text, cross);
        }

        // A removable chip is removed by a click anywhere on it: a larger target than the cross alone.
        if (!clicked)
            return ChipResult.None;
        return closable ? ChipResult.Removed : ChipResult.Clicked;
    }
}
