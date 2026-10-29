using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Data;
using GlamourBrowser.GlamourPlate;

namespace GlamourBrowser.UI.Widgets;

/// <summary>
/// The dyes an outfit or set takes, in one row: the number of dyed channels and how many of them the dresser's copies
/// already have, then the icon and count of each dye item still to use (most are "Einfacher Farbstoff", some dyes still
/// have their own). Nothing when nothing is dyed.
/// </summary>
public static class DyeCounter
{
    /// <summary>The pieces with the dyes their dresser copies already have (as the plate transfer would take them).</summary>
    public static void Draw(IEnumerable<CustomPiece> pieces, Func<uint, int> dyeCountOf, GlamourPlateBridge plates, DyeCatalog dyes, Theme.Theme theme)
        => Draw(pieces.Select(p =>
        {
            var (have0, have1) = plates.StainsInDresser(p);
            return new DyedPiece(p.Stain0, p.Stain1, dyeCountOf(p.ItemId), have0, have1);
        }), dyes, theme);

    public static void Draw(IEnumerable<DyedPiece> pieces, DyeCatalog dyes, Theme.Theme theme)
    {
        var tally = DyeNeeds.Of(pieces, dyes.ItemsOf);
        if (tally.Total == 0)
            return;

        string NameOf(uint id) => dyes.DyeItemOf(id)?.Name ?? Loc.F("Item {0}", id);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(Loc.F("Farbstoffe: {0}", tally.Total) + (tally.Done > 0 ? " " + Loc.F("({0} schon gefärbt)", tally.Done) : ""));
        if (ImGui.IsItemHovered())
        {
            var lines = tally.Needs.Select(n => $"{n.Count}× {NameOf(n.ItemId)}").ToList();
            if (tally.Done > 0)
                lines.Add(Loc.F("{0} schon so in der Kommode gefärbt", tally.Done));
            ImGui.SetTooltip(string.Join("\n", lines));
        }

        var size = new Vector2(ImGui.GetFrameHeight());
        foreach (var need in tally.Needs)
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
