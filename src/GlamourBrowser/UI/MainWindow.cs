using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

/// <summary>Filters left, tile grid in the middle, details with the live preview on the right.</summary>
public sealed class MainWindow : Window
{
    private readonly FilterPanel filters;
    private readonly TileGrid grid;
    private readonly DetailPanel details;
    private readonly Theme.Theme theme;
    private IDisposable? themeScope;

    public MainWindow(FilterPanel filters, TileGrid grid, DetailPanel details, Theme.Theme theme)
        : base("GlamourBrowser##GlamourBrowser")
    {
        this.theme = theme;
        this.filters = filters;
        this.grid = grid;
        this.details = details;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(1000, 600) };
    }

    public override void PreDraw() => themeScope = theme.Push();

    public override void PostDraw()
    {
        themeScope?.Dispose();
        themeScope = null;
    }

    public override void Draw()
    {
        lock (RenderSync.Lock)
        {
            var scale = ImGuiHelpers.GlobalScale;
            using (var child = ImRaii.Child("##filters", new Vector2(220 * scale, 0), true))
            {
                if (child.Success)
                    filters.Draw();
            }

            ImGui.SameLine();
            var detailWidth = 340 * scale;
            var gridWidth = ImGui.GetContentRegionAvail().X - detailWidth - ImGui.GetStyle().ItemSpacing.X;
            using (var child = ImRaii.Child("##grid", new Vector2(gridWidth, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (child.Success)
                    grid.Draw();
            }

            ImGui.SameLine();
            using (var child = ImRaii.Child("##details", new Vector2(detailWidth, 0), true))
            {
                if (child.Success)
                    details.Draw();
            }
        }
    }
}
