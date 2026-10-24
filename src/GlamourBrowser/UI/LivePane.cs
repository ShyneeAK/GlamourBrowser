using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>Left column: large live preview, slot bar and the selected item's card with its actions.</summary>
public sealed unsafe class LivePane
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly LivePreview live;
    private readonly CalibrationService calibration;
    private readonly JobCatalog jobs;
    private readonly Theme.Theme theme;

    public LivePane(BrowserState state, OwnershipService ownership, LivePreview live, CalibrationService calibration, JobCatalog jobs, Theme.Theme theme)
    {
        this.state = state;
        this.ownership = ownership;
        this.live = live;
        this.calibration = calibration;
        this.jobs = jobs;
        this.theme = theme;
    }

    public void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        DrawLive(new Vector2(avail.X, MathF.Min(avail.X * 960f / 576f, avail.Y * 0.55f)));

        ImGui.Spacing();
        var slots = state.Filter.Slots;
        if (SlotBar.Draw("slotbar", ref slots, theme))
            state.Filter = state.Filter with { Slots = slots };

        ImGui.Spacing();
        using var card = ImRaii.Child("##itemcard", Vector2.Zero, true);
        if (card.Success)
            DrawItemCard();
    }

    private void DrawLive(Vector2 area)
    {
        var colors = theme.Colors;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var texture = live.Texture;

        if (state.Selected == null || texture == null || texture->D3D11ShaderResourceView == null)
        {
            ImGui.Dummy(area);
            drawList.AddRectFilled(origin, origin + area, colors.Card, 8f);
            var hint = state.Selected == null ? "Item im Raster wählen" : "Live-Ansicht wird vorbereitet …";
            drawList.AddText(origin + (area - ImGui.CalcTextSize(hint)) / 2, colors.TextMuted, hint);
            return;
        }

        // Keep the CharaView aspect (576:960) inside the available area, centred horizontally.
        var height = area.Y;
        var width = MathF.Min(area.X, height * 576f / 960f);
        var size = new Vector2(width, width * 960f / 576f);
        var pos = origin + new Vector2((area.X - width) / 2, 0);
        var uv1 = new Vector2((float)texture->ActualWidth / texture->AllocatedWidth, (float)texture->ActualHeight / texture->AllocatedHeight);

        drawList.AddRectFilled(origin, origin + area, colors.Card, 8f);
        ImGui.SetCursorScreenPos(pos);
        // The invisible button captures mouse drags so the window does not move.
        ImGui.InvisibleButton("##liveview", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        drawList.AddImageRounded(new ImTextureID(texture->D3D11ShaderResourceView), pos, pos + size, Vector2.Zero, uv1, 0xFFFFFFFF, 8f);

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

        DrawHelpIcon(drawList, origin + new Vector2(area.X, 0));
        ImGui.SetCursorScreenPos(origin + new Vector2(0, area.Y + ImGui.GetStyle().ItemSpacing.Y));
    }

    /// <summary>
    /// Controls help as a small "?" in the top-right corner; the tooltip only shows over the icon,
    /// not while dragging the preview.
    /// </summary>
    private void DrawHelpIcon(ImDrawListPtr drawList, Vector2 topRight)
    {
        string glyph;
        Vector2 glyphSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            glyph = FontAwesomeIcon.QuestionCircle.ToIconString();
            glyphSize = ImGui.CalcTextSize(glyph);
        }

        var min = topRight + new Vector2(-glyphSize.X - 8, 8);
        var hovered = ImGui.IsMouseHoveringRect(min, min + glyphSize) && !ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Right);
        using (ImRaii.PushFont(UiBuilder.IconFont))
            drawList.AddText(min, hovered ? theme.Colors.Text : theme.Colors.TextMuted, glyph);
        if (hovered)
            ImGui.SetTooltip("Links ziehen: drehen\nRechts ziehen: verschieben\nMausrad: Zoom\nDoppelklick: zurücksetzen");
    }

    private void DrawItemCard()
    {
        if (state.Selected is not { } item)
        {
            ImGui.TextDisabled("Kein Item ausgewählt");
            return;
        }

        var colors = theme.Colors;
        GameIcon.Draw(item.IconId, new Vector2(40 * ImGuiHelpers.GlobalScale), FontAwesomeIcon.Tshirt, colors.TextMuted);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(item.Name);
        ImGui.TextDisabled($"Level {item.LevelEquip} · {DyeText(item.DyeCount)}");
        ImGui.EndGroup();

        DrawJobs(item);
        DrawLocations(ownership, item);

        ImGui.Separator();
        if (ImGui.Button("Anprobe"))
            Plugin.Framework.RunOnFrameworkThread(() => AgentTryon.TryOn(0, item.Id));
        ImGui.SameLine();
        if (ImGui.Button("Chat-Link"))
        {
            Plugin.ChatGui.Print(new SeStringBuilder().AddText("GlamourBrowser: ").AddItemLink(item.Id, false).Build());
            ImGui.SetClipboardText(item.Name);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Schreibt einen Item-Link in deinen eigenen Chat und kopiert den Namen.");

        if (ImGui.Button(live.WeaponDrawn ? "Waffe wegstecken" : "Waffe ziehen"))
            live.WeaponDrawn = !live.WeaponDrawn;
        ImGui.SameLine();
        if (ImGui.Button(live.WeaponVisible ? "Waffe ausblenden" : "Waffe anzeigen"))
            live.WeaponVisible = !live.WeaponVisible;

        DrawCalibration(item);
    }

    private void DrawJobs(GlamourItem item)
    {
        if (item.ClassJobIds.Count >= 40)
        {
            ImGui.TextDisabled("Alle Klassen");
            return;
        }

        var size = new Vector2(20 * ImGuiHelpers.GlobalScale);
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (size.X + 4)));
        var shown = jobs.All.Where(j => item.ClassJobIds.Contains(j.Id)).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            if (i % perRow != 0)
                ImGui.SameLine(0, 4);
            GameIcon.Draw(shown[i].IconId, size, FontAwesomeIcon.User, theme.Colors.TextMuted);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{shown[i].Abbreviation} – {shown[i].Name}");
        }
    }

    private void DrawCalibration(GlamourItem item)
    {
        var contentId = Plugin.PlayerState.ContentId;
        var slotName = SlotNames.All.FirstOrDefault(s => s.Slot == item.Slot).Name ?? item.Slot.ToString();

        if (ImGui.Button($"Kamera-Position festlegen für ({slotName})") && live.CaptureCamera() is { } preset)
        {
            calibration.Set(contentId, item.Slot, preset);
            state.InvalidateTiles();
        }

        if (calibration.Get(contentId, item.Slot) == null)
        {
            ImGui.TextDisabled($"{slotName}: Standardansicht");
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Zurücksetzen##calibration") && calibration.Remove(contentId, item.Slot))
            state.InvalidateTiles();
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

    private static string DyeText(int count) => count switch
    {
        0 => "nicht färbbar",
        1 => "1 Farbkanal",
        _ => $"{count} Farbkanäle",
    };
}
