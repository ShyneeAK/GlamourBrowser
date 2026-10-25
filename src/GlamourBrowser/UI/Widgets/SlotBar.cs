using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// Row of slot icons: click selects only that slot (again clears), Ctrl+click adds or removes. The last three icons
/// switch to the modes "Ausrüstungs-Sets", "Meine Sets" and "Wunschliste".
/// </summary>
public static class SlotBar
{
    /// <param name="reserveRight">Width kept free on the right of the row (for controls drawn after the bar).</param>
    public static bool Draw(string id, ref FilterState filter, Theme.Theme theme, float reserveRight = 0)
    {
        var slots = SlotNames.All;
        var icons = slots.Length + 3;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var edge = Math.Clamp((ImGui.GetContentRegionAvail().X - reserveRight - spacing * (icons - 1)) / icons, 21f * UiScale.Global, 38f * UiScale.Global);

        var changed = false;
        for (var i = 0; i < slots.Length; i++)
        {
            var (slot, name) = slots[i];
            if (i > 0)
                ImGui.SameLine();
            if (IconToggle.Draw($"{id}-{slot}", SlotIcons.IconFor(slot), new Vector2(edge), filter.Slots.Contains(slot), name, theme, FontAwesomeIcon.Tshirt))
            {
                filter = SlotSelection.SelectSlot(filter, slot, ImGui.GetIO().KeyCtrl);
                changed = true;
            }
        }
        ImGui.SameLine();
        if (IconToggle.Draw($"{id}-sets", SlotIcons.Sets, new Vector2(edge), filter.Mode == BrowseMode.Sets, "Ausrüstungs-Sets", theme, FontAwesomeIcon.LayerGroup))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.Sets);
            changed = true;
        }
        ImGui.SameLine();
        if (IconToggle.Draw($"{id}-mysets", SlotIcons.MySets, new Vector2(edge), filter.Mode == BrowseMode.MySets, "Meine Sets", theme, FontAwesomeIcon.Clone))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.MySets);
            changed = true;
        }
        ImGui.SameLine();
        if (IconToggle.Draw($"{id}-wishlist", SlotIcons.Wishlist, new Vector2(edge), filter.Mode == BrowseMode.Wishlist, "Wunschliste", theme, FontAwesomeIcon.Star))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.Wishlist);
            changed = true;
        }
        return changed;
    }
}
