using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// A range on one track, the part between the two handles filled. Drag a handle, or press the track to move the nearer
/// handle there; a double-click opens two fields to type the numbers.
/// </summary>
public static class RangeSlider
{
    // Per slider, from the press until the release: which handle moves and how far the press was from its centre.
    private static readonly Dictionary<string, (bool Upper, float Offset)> Grabs = new();
    // Per slider, the range before the last press: a double-click restores it, so its first click changes nothing.
    private static readonly Dictionary<string, (int Min, int Max)> BeforePress = new();

    public static bool Draw(string id, ref int min, ref int max, int lo, int hi, Theme.Theme theme)
    {
        var colors = theme.Colors;
        var height = ImGui.GetFrameHeight();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = height * 0.3f;
        var pos = ImGui.GetCursorScreenPos();
        var trackMin = pos.X + radius;
        var trackWidth = MathF.Max(1, width - radius * 2);
        float X(int value) => RangeTrack.PositionOf(value, trackMin, trackWidth, lo, hi);

        ImGui.InvisibleButton(id, new Vector2(width, height));
        var mouseX = ImGui.GetIO().MousePos.X;
        var changed = false;
        if (ImGui.IsItemActivated())
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && BeforePress.TryGetValue(id, out var before))
            {
                // The first click of the double-click may have moved a handle: undo that, then let the numbers be typed.
                changed = (min, max) != before;
                (min, max) = before;
                Grabs.Remove(id);
                ImGui.OpenPopup($"{id}-edit");
            }
            else
            {
                BeforePress[id] = (min, max);
                var upper = RangeTrack.GrabsUpper(mouseX, X(min), X(max), max == hi, min == lo);
                Grabs[id] = (upper, RangeTrack.GrabOffset(mouseX, X(upper ? max : min), radius));
            }
        }

        var active = ImGui.IsItemActive();
        bool? dragging = null;
        if (active && Grabs.TryGetValue(id, out var grab))
        {
            dragging = grab.Upper;
            var (newMin, newMax) = RangeTrack.Drag(min, max, grab.Upper, RangeTrack.ValueAt(mouseX + grab.Offset, trackMin, trackWidth, lo, hi));
            changed |= (newMin, newMax) != (min, max);
            (min, max) = (newMin, newMax);
        }
        if (!active && ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Griff ziehen oder doppelklicken zum Eintippen"));

        // Through the style alpha, so the slider fades like every other control when it is disabled.
        var drawList = ImGui.GetWindowDrawList();
        var y = pos.Y + height / 2;
        var thickness = MathF.Max(3f, height * 0.18f);
        var (minX, maxX) = (X(min), X(max));
        drawList.AddLine(new Vector2(trackMin, y), new Vector2(trackMin + trackWidth, y), ImGui.GetColorU32(colors.Border), thickness);
        drawList.AddLine(new Vector2(minX, y), new Vector2(maxX, y), ImGui.GetColorU32(colors.Accent), thickness);
        drawList.AddCircleFilled(new Vector2(minX, y), radius, ImGui.GetColorU32(dragging == false ? colors.AccentHover : colors.Accent));
        drawList.AddCircleFilled(new Vector2(maxX, y), radius, ImGui.GetColorU32(dragging == true ? colors.AccentHover : colors.Accent));

        if (ImGui.BeginPopup($"{id}-edit"))
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            // Taken on Enter only: a half-typed number must not be clamped while typing.
            var (typedMin, typedMax) = (min, max);
            ImGui.SetNextItemWidth(110 * UiScale.Global);
            if (ImGui.InputInt(Loc.T("von"), ref typedMin, 1, 10, flags: ImGuiInputTextFlags.EnterReturnsTrue))
            {
                (min, max) = RangeTrack.Typed(min, max, upper: false, typedMin, lo, hi);
                changed = true;
            }
            ImGui.SetNextItemWidth(110 * UiScale.Global);
            if (ImGui.InputInt(Loc.T("bis"), ref typedMax, 1, 10, flags: ImGuiInputTextFlags.EnterReturnsTrue))
            {
                (min, max) = RangeTrack.Typed(min, max, upper: true, typedMax, lo, hi);
                changed = true;
            }
            ImGui.TextDisabled(Loc.T("Enter übernimmt"));
            ImGui.EndPopup();
        }
        return changed;
    }
}
