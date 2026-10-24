using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

/// <summary>Middle column: paging header and the tile grid.</summary>
public sealed class TileGrid
{
    private const uint TileBackground = 0xFF201A18;
    private const uint ErrorColor = 0xFF4040E0;

    private readonly BrowserState state;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly RenderPool pool;
    private readonly OwnershipService ownership;
    private readonly LivePreview live;
    private readonly Configuration config;
    private readonly Theme.Theme theme;

    public TileGrid(BrowserState state, TileCache<IDalamudTextureWrap> cache, RenderPool pool, OwnershipService ownership,
                    LivePreview live, Configuration config, Theme.Theme theme)
    {
        this.theme = theme;
        this.state = state;
        this.cache = cache;
        this.pool = pool;
        this.ownership = ownership;
        this.live = live;
        this.config = config;
    }

    public void Draw()
    {
        DrawHeader();
        ImGui.Separator();

        if (pool.IsPaused)
        {
            ImGui.TextDisabled("Pausiert");
            return;
        }

        if (pool.Status is { } status)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), status);

        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var label = ImGui.GetTextLineHeightWithSpacing();
        state.UpdateLayout(avail.X, avail.Y, spacing, label);

        var layout = state.Layout;
        if (layout.TileWidth <= 0)
            return;

        var page = state.CurrentPage;
        for (var i = 0; i < page.Count; i++)
        {
            if (i % config.Columns != 0)
                ImGui.SameLine();
            DrawTile(page[i], new Vector2(layout.TileWidth, layout.TileHeight));
        }

        if (ImGui.IsWindowHovered() && ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
        {
            if (wheel < 0)
                state.NextPage();
            else
                state.PreviousPage();
        }
    }

    private void DrawHeader()
    {
        if (ImGui.ArrowButton("##prev", ImGuiDir.Left))
            state.PreviousPage();
        ImGui.SameLine();
        ImGui.TextUnformatted($"Seite {state.PageIndex + 1} / {state.PageCount}");
        ImGui.SameLine();
        if (ImGui.ArrowButton("##next", ImGuiDir.Right))
            state.NextPage();
        ImGui.SameLine();
        ImGui.TextDisabled($"{state.Results.Count} Treffer");

        ImGui.SameLine();
        var columns = config.Columns;
        var rows = config.Rows;
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        var changed = ImGui.SliderInt("##columns", ref columns, 2, 10, "%d Spalten");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        changed |= ImGui.SliderInt("##rows", ref rows, 1, 6, "%d Zeilen");
        if (changed)
            state.SetGrid(columns, rows);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        var mode = (int)config.TileMode;
        if (ImGui.Combo("##mode", ref mode, ["Aktuelles Outfit", "Unterwäsche"], 2))
            state.SetTileMode((TileMode)mode);
    }

    private void DrawTile(GlamourItem item, Vector2 size)
    {
        ImGui.BeginGroup();
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton($"##tile{item.Id}", size))
        {
            state.Selected = item;
            live.Select(item);
        }
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, TileBackground);

        var key = state.KeyFor(item);
        if (cache.TryGet(key, out var texture) && texture != null)
        {
            drawList.AddImage(texture.Handle, pos, pos + size);
        }
        else
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            var iconSize = new Vector2(MathF.Min(size.X * 0.5f, 64 * ImGuiHelpers.GlobalScale));
            var iconPos = pos + (size - iconSize) / 2;
            drawList.AddImage(icon.Handle, iconPos, iconPos + iconSize);
        }

        var failed = pool.Failures.TryGetValue(key, out var error);
        if (failed)
            drawList.AddText(pos + new Vector2(4, 2), ErrorColor, "!");

        var owned = ownership.Current?.IsOwned(item.Id) ?? false;
        var frame = TileStyle.FrameFor(owned, state.Selected?.Id == item.Id, theme.Colors.Accent);
        drawList.AddRect(pos, pos + size, frame.Color, 0, ImDrawFlags.None, frame.Thickness);
        // Corner triangle, top right: green when owned, gray when missing.
        var badge = MathF.Max(10f, size.X * 0.12f);
        var corner = pos + new Vector2(size.X, 0);
        drawList.AddTriangleFilled(corner, corner - new Vector2(badge, 0), corner + new Vector2(0, badge), frame.BadgeColor);

        ImGui.TextUnformatted(Truncate(item.Name, size.X));
        ImGui.EndGroup();

        if (hovered)
            DrawTooltip(item, failed ? error : null);
    }

    private void DrawTooltip(GlamourItem item, string? error)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(item.Name);
        ImGui.TextDisabled($"Level {item.LevelEquip}");
        DetailPanel.DrawLocations(ownership, item);
        if (error != null)
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), $"Renderfehler: {error}");
        ImGui.EndTooltip();
    }

    private static string Truncate(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width)
            return text;
        var length = text.Length;
        while (length > 1 && ImGui.CalcTextSize(text[..length] + "…").X > width)
            length--;
        return text[..length] + "…";
    }
}
