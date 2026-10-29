using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Data;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// The dyes an outfit or set takes, in one row: the number of dye items, then each item's icon with its count (most are
/// "Einfacher Farbstoff", some dyes still have their own). Nothing when nothing is dyed.
/// </summary>
public static class DyeCounter
{
    /// <param name="pieces">Each piece's dyes and how many dye channels its item has.</param>
    public static void Draw(IEnumerable<(byte Stain0, byte Stain1, int DyeCount)> pieces, DyeCatalog dyes, Theme.Theme theme)
    {
        var needs = DyeNeeds.Of(pieces, dyes.ItemsOf);
        if (needs.Count == 0)
            return;

        string NameOf(uint id) => dyes.DyeItemOf(id)?.Name ?? Loc.F("Item {0}", id);
        var total = needs.Sum(n => n.Count);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(Loc.F("Farbstoffe: {0}", total));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join("\n", needs.Select(n => $"{n.Count}× {NameOf(n.ItemId)}")));

        var size = new Vector2(ImGui.GetFrameHeight());
        foreach (var need in needs)
        {
            ImGui.SameLine();
            ImGui.BeginGroup();
            GameIcon.Draw(dyes.DyeItemOf(need.ItemId)?.IconId ?? 0, size, FontAwesomeIcon.Tint, theme.Colors.TextMuted);
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"×{need.Count}");
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{need.Count}× {NameOf(need.ItemId)}");
        }
    }
}
