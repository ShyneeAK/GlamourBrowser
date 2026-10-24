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

/// <summary>The tile grid: rounded cards with the name inside the image and ownership frame/badge.</summary>
public sealed class TileGrid
{
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
        if (pool.IsPaused)
        {
            ImGui.TextDisabled("Pausiert");
            return;
        }

        if (pool.Status is { } status)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), status);

        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        // Names are drawn inside the tile, so no extra label row is needed.
        state.UpdateLayout(avail.X, avail.Y, spacing, 0);

        var layout = state.Layout;
        if (layout.TileWidth <= 0)
        {
            ImGui.TextDisabled("Fenster vergrößern oder weniger Kacheln wählen");
            return;
        }

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

    private void DrawTile(GlamourItem item, Vector2 size)
    {
        const float rounding = 8f;
        var colors = theme.Colors;
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton($"##tile{item.Id}", size))
        {
            state.Selected = item;
            live.Select(item);
        }
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, hovered ? colors.CardRaised : colors.Card, rounding);

        var key = state.KeyFor(item);
        if (cache.TryGet(key, out var texture) && texture != null)
        {
            drawList.AddImageRounded(texture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, 0xFFFFFFFF, rounding);
        }
        else
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            var iconSize = new Vector2(MathF.Min(size.X * 0.5f, 64 * ImGuiHelpers.GlobalScale));
            var iconPos = pos + (size - iconSize) / 2;
            drawList.AddImage(icon.Handle, iconPos, iconPos + iconSize);
        }

        // Name on a dark gradient along the bottom edge.
        var lineHeight = ImGui.GetTextLineHeight();
        var shadeTop = pos + new Vector2(0, size.Y - lineHeight * 2.2f);
        drawList.AddRectFilledMultiColor(shadeTop, pos + size, 0x00000000, 0x00000000, 0xCC000000, 0xCC000000);
        drawList.AddText(pos + new Vector2(6, size.Y - lineHeight - 5), colors.Text, Truncate(item.Name, size.X - 12));

        var failed = pool.Failures.TryGetValue(key, out var error);
        if (failed)
            drawList.AddText(pos + new Vector2(6, 4), ErrorColor, "!");

        var owned = ownership.Current?.IsOwned(item.Id) ?? false;
        var frame = TileStyle.FrameFor(owned, state.Selected?.Id == item.Id, colors.Accent);
        drawList.AddRect(pos, pos + size, frame.Color, rounding, ImDrawFlags.None, frame.Thickness);
        // Corner triangle, top right: green when owned, gray when missing.
        var badge = MathF.Max(10f, size.X * 0.12f);
        var corner = pos + new Vector2(size.X, 0);
        drawList.AddTriangleFilled(corner + new Vector2(-1, 1), corner - new Vector2(badge, -1), corner + new Vector2(-1, badge), frame.BadgeColor);

        if (hovered)
            DrawTooltip(item, failed ? error : null);
    }

    private void DrawTooltip(GlamourItem item, string? error)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(item.Name);
        ImGui.TextDisabled($"Level {item.LevelEquip}");
        LivePane.DrawLocations(ownership, item);
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
