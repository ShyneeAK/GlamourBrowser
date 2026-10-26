using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.GlamourPlate;

namespace GlamourBrowser.UI;

/// <summary>The "In Glamour-Plate übertragen" button, a short note when all went well, and a popup only when something is wrong.</summary>
public static class PlateTransferUi
{
    private const string ResultPopup = "##plate-transfer-result";
    private static readonly TimeSpan SuccessShown = TimeSpan.FromSeconds(5);
    // The problem popup was opened: when it is closed by a click outside, it has been seen.
    private static bool resultOpened;

    /// <summary>Active only while the game's plate editor is open.</summary>
    public static void DrawButton(GlamourPlateBridge plates, IReadOnlyList<CustomPiece> pieces)
    {
        var ready = plates.IsEditorOpen && !plates.IsTransferring && pieces.Count > 0;
        using (ImRaii.Disabled(!ready))
        {
            if (ImGui.Button(Loc.T("In Glamour-Plate übertragen")))
                plates.Start(pieces);
        }
        if (!plates.IsEditorOpen && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Glamour-Plate im Spiel zum Bearbeiten öffnen"));

        if (plates.LastResult is not { HasProblems: false } done)
            return;
        if (DateTime.Now - done.Finished >= SuccessShown)
        {
            plates.ClearResult();
            return;
        }
        ImGui.SameLine();
        ImGui.TextDisabled(done.Transferred == 1 ? Loc.T("Übertragen: 1 Teil") : Loc.F("Übertragen: {0} Teile", done.Transferred));
    }

    /// <summary>Draw once per frame where it is always visible: shows what went wrong or is missing after a transfer.</summary>
    public static void DrawResult(GlamourPlateBridge plates)
    {
        if (plates.LastResult is not { HasProblems: true } result)
        {
            resultOpened = false;
            return;
        }
        if (!ImGui.IsPopupOpen(ResultPopup))
        {
            if (resultOpened)
            {
                plates.ClearResult();
                resultOpened = false;
                return;
            }
            ImGui.OpenPopup(ResultPopup);
            resultOpened = true;
        }
        if (!ImGui.BeginPopup(ResultPopup))
            return;

        if (result.EditorClosed)
            ImGui.TextUnformatted(Loc.T("Glamour-Plate ist nicht geöffnet"));
        if (!result.EditorClosed || result.Transferred > 0)
            ImGui.TextUnformatted(Loc.F("{0} von {1} Teilen übertragen", result.Transferred, result.Total));
        foreach (var (name, reason) in result.Notes)
        {
            ImGui.TextUnformatted(name);
            ImGui.SameLine();
            ImGui.TextDisabled(reason);
        }
        ImGui.Spacing();
        if (ImGui.Button("OK"))
        {
            plates.ClearResult();
            resultOpened = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
