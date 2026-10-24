using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>The tile grid: rounded cards with name, level/source line and trait icons inside the image, plus ownership frame/badge.</summary>
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
    private readonly SourceService sources;
    private readonly JobCatalog jobs;

    public TileGrid(BrowserState state, TileCache<IDalamudTextureWrap> cache, RenderPool pool, OwnershipService ownership,
                    LivePreview live, Configuration config, Theme.Theme theme, SourceService sources, JobCatalog jobs)
    {
        this.sources = sources;
        this.jobs = jobs;
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
        var owned = ownership.Current?.IsOwned(item.Id) ?? false;
        var selected = state.Selected?.Id == item.Id;
        if (cache.TryGet(key, out var texture) && texture != null)
        {
            drawList.AddImageRounded(texture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, TileStyle.ImageTint(owned, selected), rounding);
        }
        else
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            var iconSize = new Vector2(MathF.Min(size.X * 0.5f, 64 * ImGuiHelpers.GlobalScale));
            var iconPos = pos + (size - iconSize) / 2;
            drawList.AddImage(icon.Handle, iconPos, iconPos + iconSize);
        }

        // Name, level/source line and trait icons on a dark strip along the bottom edge.
        // A rounded strip (a gradient cannot be rounded and would stick out at the corners).
        var lineHeight = ImGui.GetTextLineHeight();
        var shadeTop = pos + new Vector2(0, size.Y - lineHeight * 3.4f - 8);
        drawList.AddRectFilled(shadeTop, pos + size, 0xB0000000, rounding, ImDrawFlags.RoundCornersBottom);
        var textX = 6f;
        var categories = sources.IsReady ? sources.Index.CategoriesOf(item.Id) : null;
        drawList.AddText(shadeTop + new Vector2(textX, 4), colors.Text, Truncate(item.Name, size.X - 12));
        // Owned: where it is; missing: where to get it.
        var places = owned ? ownership.Current?.LocationsOf(item.Id).Select(l => l.DisplayName).ToList() ?? [] : [];
        var line = places.Count > 0
            ? TileInfo.OwnedLine(item.LevelEquip, places)
            : TileInfo.Line(item.LevelEquip, categories != null ? SourceRanking.Top(categories) : null);
        drawList.AddText(shadeTop + new Vector2(textX, 4 + lineHeight), colors.TextMuted, Truncate(line, size.X - 12));
        DrawTraitIcons(drawList, item, categories, shadeTop + new Vector2(textX, 6 + lineHeight * 2), size.X - 12, lineHeight);

        if (pool.Failures.ContainsKey(key))
        {
            drawList.AddText(pos + new Vector2(6, 4), ErrorColor, "!");
            drawList.AddText(pos + new Vector2(6, 4 + lineHeight), ErrorColor, Truncate("Renderfehler", size.X - 12));
        }

        var frame = TileStyle.FrameFor(owned, selected, colors.Accent);
        drawList.AddRect(pos, pos + size, frame.Color, rounding, ImDrawFlags.None, frame.Thickness);
        // Corner triangle, top right: green when owned, gray when missing.
        // Inset so the tip stays inside the rounded corner (radius 8: an inset of 0.3·r lies within the arc).
        var badge = MathF.Max(10f, size.X * 0.12f);
        var inset = rounding * 0.3f;
        var corner = pos + new Vector2(size.X - inset, inset);
        drawList.AddTriangleFilled(corner, corner - new Vector2(badge, 0), corner + new Vector2(0, badge), frame.BadgeColor);
    }

    /// <summary>Dye channels, tradability, job/role and one source icon per group; cut off on the right when the tile is narrow.</summary>
    private void DrawTraitIcons(ImDrawListPtr drawList, GlamourItem item, IReadOnlySet<SourceCategory>? categories, Vector2 min, float width, float iconSize)
    {
        var colors = theme.Colors;
        var icons = new List<Action<Vector2>>();
        if (item.DyeCount > 0)
            icons.Add(at =>
            {
                GameIcon.DrawAt(drawList, 0, at, new Vector2(iconSize), FontAwesomeIcon.PaintBrush, colors.TextMuted);
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, at + new Vector2(iconSize * 0.6f, iconSize * 0.35f), colors.Text, item.DyeCount.ToString());
            });
        if (item.Trade != Tradability.Bound)
        {
            var glyph = item.Trade == Tradability.Marketable ? FontAwesomeIcon.Store : FontAwesomeIcon.ExchangeAlt;
            icons.Add(at => GameIcon.DrawAt(drawList, 0, at, new Vector2(iconSize), glyph, colors.TextMuted));
        }
        var badge = JobBadges.For(item.ClassJobIds, jobs.GroupOf, jobs.All.Count);
        if (badge.Kind != JobBadgeKind.None)
        {
            var icon = badge.Kind == JobBadgeKind.Job ? jobs.Find(badge.JobId)?.IconId ?? 0 : JobCatalog.RoleIcon(badge.Group);
            icons.Add(at => GameIcon.DrawAt(drawList, icon, at, new Vector2(iconSize), FontAwesomeIcon.User, colors.TextMuted));
        }
        if (categories != null)
        {
            foreach (var category in SourceRanking.Icons(categories))
                icons.Add(at => DrawSourceIcon(drawList, category, at, iconSize, colors.TextMuted));
        }

        const float spacing = 3f;
        var fit = TileInfo.FitIcons(icons.Count, iconSize, spacing, width);
        for (var i = 0; i < fit; i++)
            icons[i](min + new Vector2(i * (iconSize + spacing), 0));
    }

    /// <summary>A source category icon; savage gets a red corner because the game has no own icon for it.</summary>
    public static void DrawSourceIcon(ImDrawListPtr drawList, SourceCategory category, Vector2 min, float size, uint fallbackColor)
    {
        GameIcon.DrawAt(drawList, SourceIcons.IconFor(category), min, new Vector2(size), SourceIcons.Fallback(category), fallbackColor);
        if (!SourceIcons.HasSavageMark(category))
            return;
        var corner = min + new Vector2(size, 0);
        var mark = size * 0.45f;
        drawList.AddTriangleFilled(corner, corner - new Vector2(mark, 0), corner + new Vector2(0, mark), 0xFF2020E0);
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
