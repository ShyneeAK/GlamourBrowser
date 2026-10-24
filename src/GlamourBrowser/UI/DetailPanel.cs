using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.UI;

/// <summary>Right column: details and the live preview of the selected item.</summary>
public sealed unsafe class DetailPanel
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly LivePreview live;
    private readonly Configuration config;
    private readonly Dictionary<uint, string> jobAbbreviations;

    public DetailPanel(BrowserState state, OwnershipService ownership, LivePreview live, Configuration config)
    {
        this.state = state;
        this.ownership = ownership;
        this.live = live;
        this.config = config;
        jobAbbreviations = Plugin.DataManager.GetExcelSheet<ClassJob>().ToDictionary(j => j.RowId, j => j.Abbreviation.ExtractText());
    }

    public void Draw()
    {
        if (state.Selected is not { } item)
        {
            ImGui.TextDisabled("Item auswählen");
            return;
        }

        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(40 * ImGuiHelpers.GlobalScale));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(item.Name);
        ImGui.TextDisabled($"Level {item.LevelEquip} · {DyeText(item.DyeCount)}");
        ImGui.EndGroup();

        ImGui.TextWrapped(JobText(item));
        DrawLocations(ownership, item);
        ImGui.Separator();
        DrawLive();
        DrawCalibration(item);
    }

    private void DrawCalibration(GlamourItem item)
    {
        var contentId = Plugin.PlayerState.ContentId;
        var calibrations = new CameraCalibrations(config.CameraCalibrations);
        var slotName = SlotNames.All.FirstOrDefault(s => s.Slot == item.Slot).Name ?? item.Slot.ToString();

        if (ImGui.Button($"Kamera-Position festlegen für ({slotName})") && live.CaptureCamera() is { } preset)
        {
            calibrations.Set(contentId, item.Slot, preset);
            config.Save();
            state.InvalidateTiles();
        }

        if (calibrations.Get(contentId, item.Slot) == null)
        {
            ImGui.TextDisabled($"{slotName}: Standardansicht");
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Zurücksetzen##calibration") && calibrations.Remove(contentId, item.Slot))
        {
            config.Save();
            state.InvalidateTiles();
        }
        ImGui.TextDisabled($"{slotName}: kalibriert");
    }

    public static void DrawLocations(OwnershipService ownership, GlamourItem item)
    {
        var locations = ownership.Current?.LocationsOf(item.Id) ?? [];
        if (locations.Count == 0)
        {
            ImGui.TextDisabled("Nicht im Besitz");
            return;
        }
        foreach (var l in locations)
            ImGui.TextUnformatted($"Fundort: {l.DisplayName} (Stand {l.Seen:dd.MM. HH:mm})");
    }

    private void DrawLive()
    {
        var texture = live.Texture;
        var width = ImGui.GetContentRegionAvail().X;
        if (texture == null || texture->D3D11ShaderResourceView == null)
        {
            ImGui.TextDisabled("Live-Ansicht wird vorbereitet …");
            return;
        }

        var size = new Vector2(width, width * texture->ActualHeight / texture->ActualWidth);
        var uv1 = new Vector2((float)texture->ActualWidth / texture->AllocatedWidth, (float)texture->ActualHeight / texture->AllocatedHeight);

        // The invisible button captures mouse drags so the window does not move.
        var pos = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##liveview", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        ImGui.GetWindowDrawList().AddImage(new ImTextureID(texture->D3D11ShaderResourceView), pos, pos + size, Vector2.Zero, uv1);

        var io = ImGui.GetIO();
        if (ImGui.IsItemActive())
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                live.QueueRotate(io.MouseDelta.X, io.MouseDelta.Y);
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
                live.QueuePan(io.MouseDelta.X, io.MouseDelta.Y);
        }

        if (ImGui.IsItemHovered())
        {
            if (io.MouseWheel != 0)
                live.QueueZoom(io.MouseWheel);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                live.QueueReset();
        }

        if (ImGui.Button(live.WeaponDrawn ? "Waffe wegstecken" : "Waffe ziehen"))
            live.WeaponDrawn = !live.WeaponDrawn;
        ImGui.SameLine();
        if (ImGui.Button(live.WeaponVisible ? "Waffe ausblenden" : "Waffe anzeigen"))
            live.WeaponVisible = !live.WeaponVisible;
        ImGui.TextDisabled("Links ziehen: drehen · Rechts ziehen: verschieben · Mausrad: Zoom · Doppelklick: zurücksetzen");
    }

    private string JobText(GlamourItem item)
    {
        if (item.ClassJobIds.Count >= jobAbbreviations.Count - 1)
            return "Alle Klassen";
        return string.Join(" ", item.ClassJobIds.Select(id => jobAbbreviations.GetValueOrDefault(id, "?")));
    }

    private static string DyeText(int count) => count switch
    {
        0 => "nicht färbbar",
        1 => "1 Farbkanal",
        _ => $"{count} Farbkanäle",
    };
}
