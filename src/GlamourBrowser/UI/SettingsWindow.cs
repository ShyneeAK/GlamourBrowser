using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

public sealed class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly BrowserState state;
    private readonly RenderPool pool;
    private int presetSlot = 2; // index into SlotNames.All, starts at "Kopf"

    public SettingsWindow(Configuration config, BrowserState state, RenderPool pool)
        : base("GlamourBrowser – Einstellungen##GlamourBrowserSettings")
    {
        this.config = config;
        this.state = state;
        this.pool = pool;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 380) };
    }

    public override void Draw()
    {
        var columns = config.Columns;
        var rows = config.Rows;
        var gridChanged = ImGui.SliderInt("Spalten", ref columns, 2, 10);
        gridChanged |= ImGui.SliderInt("Zeilen", ref rows, 1, 6);
        if (gridChanged)
            state.SetGrid(columns, rows);

        var mode = (int)config.TileMode;
        if (ImGui.Combo("Kachel-Modus", ref mode, ["Aktuelles Outfit", "Unterwäsche"], 2))
            state.SetTileMode((TileMode)mode);

        var views = config.ParallelViews;
        if (ImGui.SliderInt("Parallele Ansichten", ref views, 1, 3))
        {
            config.ParallelViews = views;
            pool.MaxViews = views;
            config.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Live-Ansicht");
        var rotate = config.RotateSensitivity;
        var pan = config.PanSensitivity;
        var zoom = config.ZoomSensitivity;
        var changed = ImGui.SliderFloat("Drehen", ref rotate, 0.01f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        changed |= ImGui.SliderFloat("Verschieben", ref pan, 0.01f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        changed |= ImGui.SliderFloat("Zoom", ref zoom, 0.1f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        if (changed)
        {
            config.RotateSensitivity = rotate;
            config.PanSensitivity = pan;
            config.ZoomSensitivity = zoom;
            config.Save();
        }

        ImGui.Separator();
        DrawCameraPresets();
    }

    private void DrawCameraPresets()
    {
        ImGui.TextDisabled("Kamera-Presets (wirken sofort auf die Live-Ansicht, wenn das gewählte Item zum Slot passt)");
        ImGui.Combo("Slot", ref presetSlot, SlotNames.All.Select(s => s.Name).ToArray(), SlotNames.All.Length);
        var slot = SlotNames.All[presetSlot].Slot;
        var p = CameraPresets.Resolve(slot, config.CameraOverrides);
        float yaw = p.Yaw, pitch = p.Pitch, distance = p.Distance, panX = p.PanX, panY = p.PanY;

        var changed = ImGui.DragFloat("Yaw", ref yaw, 0.01f);
        var done = ImGui.IsItemDeactivatedAfterEdit();
        changed |= ImGui.DragFloat("Pitch", ref pitch, 0.01f);
        done |= ImGui.IsItemDeactivatedAfterEdit();
        changed |= ImGui.DragFloat("Distance", ref distance, 0.05f);
        done |= ImGui.IsItemDeactivatedAfterEdit();
        changed |= ImGui.DragFloat("PanX", ref panX, 0.01f);
        done |= ImGui.IsItemDeactivatedAfterEdit();
        changed |= ImGui.DragFloat("PanY", ref panY, 0.01f);
        done |= ImGui.IsItemDeactivatedAfterEdit();

        // The live view follows every change; tiles are only re-rendered once the drag ends.
        if (changed)
            config.CameraOverrides[slot] = new CameraPreset(yaw, pitch, distance, panX, panY);
        if (done)
        {
            config.Save();
            state.InvalidateTiles();
        }

        if (ImGui.Button("Zurücksetzen") && config.CameraOverrides.Remove(slot))
        {
            config.Save();
            state.InvalidateTiles();
        }
    }
}
