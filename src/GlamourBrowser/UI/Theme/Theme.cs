using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.UI.Theme;

/// <summary>
/// The plugin's modern dark theme. Only applied inside our own windows: <see cref="Push"/> in
/// Window.PreDraw, dispose the returned scope in Window.PostDraw.
/// </summary>
public sealed class Theme
{
    private static readonly (ImGuiCol Col, Func<ThemeColors, uint> Pick)[] ColorMap =
    [
        (ImGuiCol.WindowBg, c => c.WindowBg),
        (ImGuiCol.ChildBg, c => c.WindowBg),
        (ImGuiCol.PopupBg, c => c.CardRaised),
        (ImGuiCol.Border, c => c.Border),
        (ImGuiCol.FrameBg, c => c.Card),
        (ImGuiCol.FrameBgHovered, c => c.CardRaised),
        (ImGuiCol.FrameBgActive, c => c.AccentMuted),
        (ImGuiCol.Button, c => c.Card),
        (ImGuiCol.ButtonHovered, c => c.CardRaised),
        (ImGuiCol.ButtonActive, c => c.AccentMuted),
        (ImGuiCol.Header, c => c.Card),
        (ImGuiCol.HeaderHovered, c => c.CardRaised),
        (ImGuiCol.HeaderActive, c => c.AccentMuted),
        (ImGuiCol.CheckMark, c => c.Accent),
        (ImGuiCol.SliderGrab, c => c.Accent),
        (ImGuiCol.SliderGrabActive, c => c.AccentHover),
        (ImGuiCol.Text, c => c.Text),
        (ImGuiCol.TextDisabled, c => c.TextMuted),
        (ImGuiCol.TitleBg, c => c.WindowBg),
        (ImGuiCol.TitleBgActive, c => c.Card),
    ];

    private static readonly (ImGuiStyleVar Var, float Value)[] FloatVars =
    [
        (ImGuiStyleVar.WindowRounding, 10f),
        (ImGuiStyleVar.ChildRounding, 8f),
        (ImGuiStyleVar.FrameRounding, 6f),
        (ImGuiStyleVar.PopupRounding, 8f),
        (ImGuiStyleVar.GrabRounding, 6f),
    ];

    private static readonly (ImGuiStyleVar Var, Vector2 Value)[] VectorVars =
    [
        (ImGuiStyleVar.FramePadding, new Vector2(8, 5)),
        (ImGuiStyleVar.ItemSpacing, new Vector2(8, 6)),
    ];

    private readonly Configuration config;

    public Theme(Configuration config)
    {
        this.config = config;
        Refresh();
    }

    public ThemeColors Colors { get; private set; }

    /// <summary>Re-reads the accent colour from the configuration.</summary>
    public void Refresh() => Colors = ThemePalette.For(config.Accent);

    public IDisposable Push()
    {
        foreach (var (col, pick) in ColorMap)
            ImGui.PushStyleColor(col, pick(Colors));
        foreach (var (var, value) in FloatVars)
            ImGui.PushStyleVar(var, value);
        foreach (var (var, value) in VectorVars)
            ImGui.PushStyleVar(var, value);
        return new Scope(ColorMap.Length, FloatVars.Length + VectorVars.Length);
    }

    private sealed class Scope(int colors, int vars) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            ImGui.PopStyleVar(vars);
            ImGui.PopStyleColor(colors);
        }
    }
}
