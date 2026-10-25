using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.GlamourPlate;

namespace GlamourBrowser.UI;

/// <summary>The "In Glamour-Plate übertragen" button and the popup with the outcome.</summary>
public static class PlateTransferUi
{
    private const string ResultPopup = "##plate-transfer-result";

    /// <summary>Active only while the game's plate editor is open; the outcome shows in <see cref="DrawResult"/>.</summary>
    public static void DrawButton(GlamourPlateBridge plates, IReadOnlyList<CustomPiece> pieces)
    {
        var ready = plates.IsEditorOpen && !plates.IsTransferring && pieces.Count > 0;
        using (ImRaii.Disabled(!ready))
        {
            if (ImGui.Button("In Glamour-Plate übertragen"))
                plates.Start(pieces);
        }
        if (!plates.IsEditorOpen && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Glamour-Plate im Spiel zum Bearbeiten öffnen");
    }

    /// <summary>Draw once per frame where it is always visible: opens the result when a transfer has finished.</summary>
    public static void DrawResult(GlamourPlateBridge plates)
    {
        if (plates.LastResult is not { } result)
            return;
        if (!ImGui.IsPopupOpen(ResultPopup))
            ImGui.OpenPopup(ResultPopup);
        if (!ImGui.BeginPopupModal(ResultPopup, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            return;

        if (result.EditorClosed)
            ImGui.TextUnformatted("Glamour-Plate ist nicht geöffnet");
        if (!result.EditorClosed || result.Transferred > 0)
            ImGui.TextUnformatted($"{result.Transferred} von {result.Total} Teilen übertragen");
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
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
