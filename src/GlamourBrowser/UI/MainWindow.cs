using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

/// <summary>WoW-style layout: live pane left; top bar, tile grid and optional filter column right.</summary>
public sealed class MainWindow : Window
{
    private const float FilterColumnWidth = 220f;
    private const float MinGridWidth = 120f;

    private readonly LivePane livePane;
    private readonly TopBar topBar;
    private readonly FilterColumn filterColumn;
    private readonly TileGrid grid;
    private readonly Theme.Theme theme;
    private readonly Configuration config;
    private IDisposable? themeScope;

    public MainWindow(LivePane livePane, TopBar topBar, FilterColumn filterColumn, TileGrid grid, Theme.Theme theme, Configuration config)
        : base("GlamourBrowser##GlamourBrowser")
    {
        this.livePane = livePane;
        this.topBar = topBar;
        this.filterColumn = filterColumn;
        this.grid = grid;
        this.theme = theme;
        this.config = config;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(1100, 650) };
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
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var total = ImGui.GetContentRegionAvail().X;
            var left = MathF.Max(300 * scale, total * 0.32f);

            using (var child = ImRaii.Child("##livepane", new Vector2(left, 0), false))
            {
                if (child.Success)
                    livePane.Draw();
            }

            ImGui.SameLine();
            using var right = ImRaii.Child("##right", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (!right.Success)
                return;

            topBar.Draw();
            ImGui.Separator();

            var filterWidth = config.FilterColumnVisible ? FilterColumnWidth * scale : 0;
            var gridWidth = MathF.Max(0, ImGui.GetContentRegionAvail().X - filterWidth - (filterWidth > 0 ? spacing : 0));
            if (gridWidth < MinGridWidth * scale)
            {
                ImGui.TextDisabled("Fenster vergrößern oder die Filterspalte ausblenden");
            }
            else
            {
                using var gridChild = ImRaii.Child("##grid", new Vector2(gridWidth, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                if (gridChild.Success)
                    grid.Draw();
            }

            if (filterWidth > 0)
            {
                ImGui.SameLine();
                using var filters = ImRaii.Child("##filters", new Vector2(filterWidth, 0), true);
                if (filters.Success)
                    filterColumn.Draw();
            }
        }
    }
}
