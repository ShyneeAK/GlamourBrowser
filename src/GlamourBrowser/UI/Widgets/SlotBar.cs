using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>Row of slot icons: click selects only that slot (again clears), Ctrl+click adds or removes.</summary>
public static class SlotBar
{
    public static bool Draw(string id, ref IReadOnlySet<EquipSlot> selected, Theme.Theme theme)
    {
        var slots = SlotNames.All;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var edge = Math.Clamp((ImGui.GetContentRegionAvail().X - spacing * (slots.Length - 1)) / slots.Length, 21f * UiScale.Global, 38f * UiScale.Global);

        var changed = false;
        for (var i = 0; i < slots.Length; i++)
        {
            var (slot, name) = slots[i];
            if (i > 0)
                ImGui.SameLine();
            if (IconToggle.Draw($"{id}-{slot}", SlotIcons.IconFor(slot), new Vector2(edge), selected.Contains(slot), name, theme, FontAwesomeIcon.Tshirt))
            {
                selected = SlotSelection.Toggle(selected, slot, ImGui.GetIO().KeyCtrl);
                changed = true;
            }
        }
        return changed;
    }
}
