using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

public sealed class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly BrowserState state;
    private readonly RenderPool pool;
    private readonly CalibrationService calibration;

    public SettingsWindow(Configuration config, BrowserState state, RenderPool pool, CalibrationService calibration)
        : base("GlamourBrowser – Einstellungen##GlamourBrowserSettings")
    {
        this.config = config;
        this.state = state;
        this.pool = pool;
        this.calibration = calibration;
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
        ImGui.TextDisabled("Kamera-Kalibrierung dieses Charakters");
        ImGui.TextWrapped("Kalibriert wird in der Live-Ansicht: Item eines Slots auswählen, Ansicht einstellen, „Kamera-Position festlegen“ klicken.");

        var contentId = Plugin.PlayerState.ContentId;
        calibration.Ensure(contentId);
        var calibrated = calibration.Calibrations.For(contentId);
        foreach (var (slot, name) in SlotNames.All)
        {
            if (!calibrated.ContainsKey(slot))
            {
                ImGui.TextDisabled($"{name}: Standard");
                continue;
            }

            ImGui.TextUnformatted($"{name}: kalibriert");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Zurücksetzen##{slot}") && calibration.Remove(contentId, slot))
                state.InvalidateTiles();
        }
    }
}
