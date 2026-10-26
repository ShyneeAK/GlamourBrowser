using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// Row of slot icons: click selects only that slot (again clears), Ctrl+click adds or removes. The last icon switches to
/// the mode "Ausrüstungs-Sets"; "Meine Sets" and "Wunschliste" sit in a second row (<see cref="DrawModes"/>).
/// </summary>
public static class SlotBar
{
    /// <summary>The icon size for the row, so the second row can use the same.</summary>
    /// <param name="reserveRight">Width kept free on the right of the row (for controls drawn after the bar).</param>
    public static float Edge(float reserveRight = 0)
    {
        var icons = SlotNames.All.Length + 1;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return Math.Clamp((ImGui.GetContentRegionAvail().X - reserveRight - spacing * (icons - 1)) / icons, 21f * UiScale.Global, 38f * UiScale.Global);
    }

    public static bool Draw(string id, ref FilterState filter, Theme.Theme theme, float edge)
    {
        var slots = SlotNames.All;

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
            if (slot is EquipSlot.RingRight or EquipSlot.RingLeft)
                DrawHandMark(Loc.T(slot == EquipSlot.RingRight ? "R" : "L"));
        }
        ImGui.SameLine();
        if (IconToggle.Draw($"{id}-sets", SlotIcons.Sets, new Vector2(edge), filter.Mode == BrowseMode.Sets, Loc.T("Ausrüstungs-Sets"), theme, FontAwesomeIcon.LayerGroup))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.Sets);
            changed = true;
        }
        return changed;
    }

    /// <summary>The hand of a ring slot, small in the icon's bottom-right corner (both ring slots share the game's icon).</summary>
    private static void DrawHandMark(string hand)
    {
        var drawList = ImGui.GetWindowDrawList();
        var at = ImGui.GetItemRectMax() - ImGui.CalcTextSize(hand) - new Vector2(3, 1);
        drawList.AddText(at + Vector2.One, 0xC0000000, hand);
        drawList.AddText(at, 0xFFFFFFFF, hand);
    }

    /// <summary>The modes "Meine Sets" and "Wunschliste", for the row under the slot icons.</summary>
    public static bool DrawModes(string id, ref FilterState filter, Theme.Theme theme, float edge)
    {
        var changed = false;
        if (IconToggle.Draw($"{id}-mysets", SlotIcons.MySets, new Vector2(edge), filter.Mode == BrowseMode.MySets, Loc.T("Meine Sets"), theme, FontAwesomeIcon.Clone))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.MySets);
            changed = true;
        }
        ImGui.SameLine();
        if (IconToggle.Draw($"{id}-wishlist", SlotIcons.Wishlist, new Vector2(edge), filter.Mode == BrowseMode.Wishlist, Loc.T("Wunschliste"), theme, FontAwesomeIcon.Star))
        {
            filter = SlotSelection.ToggleMode(filter, BrowseMode.Wishlist);
            changed = true;
        }
        return changed;
    }
}
