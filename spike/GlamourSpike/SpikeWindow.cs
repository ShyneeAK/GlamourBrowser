using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace GlamourSpike;

public sealed unsafe class SpikeWindow : Window
{
    private static readonly int[] TileCounts = [12, 24, 48, 96];

    private readonly SpikeController controller;
    private int index = 7;
    private int parallelViews = 3;
    private int settleFrames = 1;
    private int tileCountChoice;
    private float rotateSensitivity = 0.5f;
    private float panSensitivity = 0.05f;
    private float zoomSensitivity = 3.0f;

    public SpikeWindow(SpikeController controller)
        : base("GlamourBrowser Spike##GlamourSpike")
    {
        this.controller = controller;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 650) };
    }

    // Anything that touches the CharaView runs on the framework thread.
    private static void OnFramework(Action action) => Plugin.Framework.RunOnFrameworkThread(action);

    public override void Draw()
    {
        DrawToolbar();
        ImGui.Separator();

        if (!ImGui.BeginTable("##layout", 2, ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupColumn("live", ImGuiTableColumnFlags.WidthFixed, 340 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("grid", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawLive();
        ImGui.Spacing();
        DrawMemory();
        ImGui.TableNextColumn();
        using (var child = ImRaii.Child("##grid"))
        {
            if (child.Success)
                DrawGrid();
        }
        ImGui.EndTable();
    }

    private void DrawToolbar()
    {
        ImGui.Text($"Status: {controller.Status} | CharaView-State: {controller.ViewState} | idle: {controller.ViewIdle} | Frame {Plugin.Framework.UpdateDelta.TotalMilliseconds:F1} ms");

        using (ImRaii.Disabled(controller.IsCreated))
        {
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("Platz (0-7)", ref index))
                index = Math.Clamp(index, 0, 7);
            ImGui.SameLine();
            if (ImGui.Button("Start"))
            {
                controller.Index = (uint)index;
                OnFramework(controller.Start);
            }
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!controller.IsCreated))
        {
            if (ImGui.Button("Stop"))
                OnFramework(controller.Stop);
            ImGui.SameLine();
            if (ImGui.Button("Eigene Ausruestung"))
                OnFramework(controller.ResetToOwnGear);
            ImGui.SameLine();
            using (ImRaii.Disabled(controller.IsGridRunning || !controller.ViewIdle))
            {
                if (ImGui.Button("Raster rendern"))
                {
                    controller.ParallelViews = parallelViews;
                    controller.SettleFrames = settleFrames;
                    OnFramework(controller.StartGrid);
                }
            }
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(controller.IsGridRunning))
        {
            if (ImGui.Button("Kacheln freigeben"))
                OnFramework(controller.ReleaseTiles);
        }

        using (ImRaii.Disabled(controller.IsGridRunning))
        {
            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            ImGui.SliderInt("Parallele Ansichten", ref parallelViews, 1, 3);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            ImGui.SliderInt("Wartezeit (Frames)", ref settleFrames, 0, 10);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo("Kacheln", ref tileCountChoice, TileCounts.Select(c => c.ToString()).ToArray(), TileCounts.Length))
                OnFramework(() => controller.PickItems(TileCounts[tileCountChoice]));
        }

        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Drehen", ref rotateSensitivity, 0.01f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Verschieben", ref panSensitivity, 0.001f, 5f, "%.3f", ImGuiSliderFlags.Logarithmic);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Zoom", ref zoomSensitivity, 0.1f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);

        var done = controller.Tiles.Where(t => t.Milliseconds > 0).ToList();
        if (done.Count > 0)
        {
            var total = controller.TotalMilliseconds;
            ImGui.Text($"Kacheln fertig: {done.Count}/{controller.Tiles.Count} | pro Kachel Schnitt {done.Average(t => t.Milliseconds):F0} ms, Max {done.Max(t => t.Milliseconds):F0} ms | Lade-Frames Schnitt {done.Average(t => t.LoadFrames):F1}"
                       + (total is { } t ? $" | Gesamt {t:F0} ms = {t / done.Count:F0} ms je Kachel effektiv" : string.Empty));
        }
    }

    private void DrawLive()
    {
        ImGui.Text(controller.LiveItemId is { } id ? $"Live: Item {id}" : "Live: eigene Ausruestung");
        var texture = controller.View != null ? controller.View.GetTexture() : null;
        if (texture == null || texture->D3D11ShaderResourceView == null)
        {
            ImGui.TextDisabled("(keine Live-Textur)");
            return;
        }

        var width = 320 * ImGuiHelpers.GlobalScale;
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
                controller.QueueRotate(io.MouseDelta.X * rotateSensitivity, io.MouseDelta.Y * rotateSensitivity);
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
                controller.QueuePan(io.MouseDelta.X * panSensitivity, -io.MouseDelta.Y * panSensitivity);
        }

        if (ImGui.IsItemHovered())
        {
            if (io.MouseWheel != 0)
                controller.QueueZoom(-io.MouseWheel * zoomSensitivity);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                controller.QueueCameraReset();
        }

        ImGui.TextDisabled("Links ziehen: drehen | Rechts ziehen: verschieben");
        ImGui.TextDisabled("Mausrad: Zoom | Doppelklick: Kamera zuruecksetzen");
        ImGui.TextDisabled($"Textur {texture->ActualWidth}x{texture->ActualHeight} ({texture->TextureFormat})");
    }

    private void DrawMemory()
    {
        ImGui.Text($"Kachel-Texturen: {MemoryProbe.Mb(controller.TileBytes)} VRAM (berechnet)");
        if (ImGui.Button("Speicher jetzt messen"))
            controller.Memory.Snapshot("Manuell");

        var samples = controller.Memory.Samples;
        if (samples.Count == 0 || !ImGui.BeginTable("##memory", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("Zeitpunkt");
        ImGui.TableSetupColumn("RAM privat (+/-)");
        ImGui.TableSetupColumn("VRAM (+/-)");
        ImGui.TableHeadersRow();
        var first = samples[0];
        foreach (var s in samples)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(s.Label);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{MemoryProbe.Mb(s.PrivateBytes)} ({Delta(s.PrivateBytes, first.PrivateBytes)})");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(s.VramLocal < 0 ? "n/a" : $"{MemoryProbe.Mb(s.VramLocal)} ({Delta(s.VramLocal, first.VramLocal)})");
        }
        ImGui.EndTable();
    }

    private static string Delta(long value, long baseline) => $"{(value - baseline) / (1024.0 * 1024.0):+0.0;-0.0;0.0}";

    private void DrawGrid()
    {
        var tileWidth = 150 * ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + spacing) / (tileWidth + spacing)));

        for (var i = 0; i < controller.Tiles.Count; i++)
        {
            var tile = controller.Tiles[i];
            if (i % columns != 0)
                ImGui.SameLine();

            ImGui.BeginGroup();
            var size = new Vector2(tileWidth, tileWidth * 5 / 3);
            if (tile.Texture != null)
            {
                size = new Vector2(tileWidth, tileWidth * tile.Texture.Height / tile.Texture.Width);
                ImGui.Image(tile.Texture.Handle, size);
            }
            else
            {
                ImGui.Button($"{tile.Error ?? "..."}##tile{i}", size);
            }

            if (ImGui.IsItemClicked())
                OnFramework(() => controller.ShowLive(tile.ItemId));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{tile.Name} ({tile.ItemId})\n{tile.Milliseconds:F0} ms, {tile.LoadFrames} Lade-Frames, Platz {tile.ViewIndex}{(tile.Error != null ? "\n" + tile.Error : string.Empty)}");

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + tileWidth);
            ImGui.TextUnformatted(tile.Name);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
        }
    }
}
