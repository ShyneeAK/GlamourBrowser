using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// A range on one track, the part between the two handles filled. Drag a handle, or click the track to move the nearer
/// handle there; a double-click opens two fields to type the numbers.
/// </summary>
public static class RangeSlider
{
    // Which handle a slider drags, from the click until the mouse button is released.
    private static readonly Dictionary<string, bool> GrabbedUpper = new();

    public static bool Draw(string id, ref int min, ref int max, int lo, int hi, Theme.Theme theme)
    {
        var colors = theme.Colors;
        var height = ImGui.GetFrameHeight();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = height * 0.3f;
        var pos = ImGui.GetCursorScreenPos();
        var trackMin = pos.X + radius;
        var trackWidth = MathF.Max(1, width - radius * 2);

        ImGui.InvisibleButton(id, new Vector2(width, height));
        var mouseX = ImGui.GetIO().MousePos.X;
        if (ImGui.IsItemActivated())
        {
            GrabbedUpper[id] = RangeTrack.GrabsUpper(mouseX, RangeTrack.PositionOf(min, trackMin, trackWidth, lo, hi),
                                                     RangeTrack.PositionOf(max, trackMin, trackWidth, lo, hi));
        }

        var changed = false;
        var active = ImGui.IsItemActive();
        bool? dragging = active && GrabbedUpper.TryGetValue(id, out var upper) ? upper : null;
        if (dragging is { } draggingUpper)
        {
            var (newMin, newMax) = RangeTrack.Drag(min, max, draggingUpper, RangeTrack.ValueAt(mouseX, trackMin, trackWidth, lo, hi));
            changed = newMin != min || newMax != max;
            (min, max) = (newMin, newMax);
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            ImGui.OpenPopup($"{id}-edit");
        if (!active && ImGui.IsItemHovered())
            ImGui.SetTooltip("Griff ziehen oder doppelklicken zum Eintippen");

        var drawList = ImGui.GetWindowDrawList();
        var y = pos.Y + height / 2;
        var thickness = MathF.Max(3f, height * 0.18f);
        var minX = RangeTrack.PositionOf(min, trackMin, trackWidth, lo, hi);
        var maxX = RangeTrack.PositionOf(max, trackMin, trackWidth, lo, hi);
        drawList.AddLine(new Vector2(trackMin, y), new Vector2(trackMin + trackWidth, y), colors.Border, thickness);
        drawList.AddLine(new Vector2(minX, y), new Vector2(maxX, y), colors.Accent, thickness);
        drawList.AddCircleFilled(new Vector2(minX, y), radius, dragging == false ? colors.AccentHover : colors.Accent);
        drawList.AddCircleFilled(new Vector2(maxX, y), radius, dragging == true ? colors.AccentHover : colors.Accent);

        if (ImGui.BeginPopup($"{id}-edit"))
        {
            // Taken on Enter only: a half-typed number must not be clamped while typing.
            var (typedMin, typedMax) = (min, max);
            ImGui.SetNextItemWidth(110 * UiScale.Global);
            if (ImGui.InputInt("von", ref typedMin, 1, 10, flags: ImGuiInputTextFlags.EnterReturnsTrue))
            {
                min = Math.Clamp(typedMin, lo, max);
                changed = true;
            }
            ImGui.SetNextItemWidth(110 * UiScale.Global);
            if (ImGui.InputInt("bis", ref typedMax, 1, 10, flags: ImGuiInputTextFlags.EnterReturnsTrue))
            {
                max = Math.Clamp(typedMax, min, hi);
                changed = true;
            }
            ImGui.TextDisabled("Enter übernimmt");
            ImGui.EndPopup();
        }
        return changed;
    }
}
