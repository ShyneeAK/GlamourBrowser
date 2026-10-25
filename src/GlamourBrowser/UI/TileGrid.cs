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
using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>The tile grid: rounded cards with name, level/source line and trait icons inside the image, plus ownership frame/badge; page controls below.</summary>
public sealed class TileGrid
{
    private const uint ErrorColor = 0xFF4040E0;
    // Trait and source icons at the bottom of a tile, relative to the line height.
    private const float TraitIconScale = 1.5f;

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
        // Slot selection sits above the tiles it filters, the tile display options right-aligned next to it.
        var slots = state.Filter.Slots;
        if (SlotBar.Draw("slotbar", ref slots, theme, DisplayOptionsWidth()))
            state.Filter = state.Filter with { Slots = slots };
        DrawDisplayOptions();
        ImGui.Spacing();

        if (pool.IsPaused)
        {
            ImGui.TextDisabled("Pausiert");
            return;
        }

        if (pool.Status is { } status)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), status);

        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        // Names are drawn inside the tile, so no extra label row is needed; the page controls below take one row.
        var pagerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
        state.UpdateLayout(avail.X, avail.Y - pagerHeight, spacing, 0);

        var layout = state.Layout;
        if (layout.TileWidth <= 0)
        {
            ImGui.TextDisabled("Fenster vergrößern oder weniger Kacheln wählen");
            return;
        }

        var gridTopLeft = ImGui.GetCursorPos();
        var page = state.CurrentPage;
        for (var i = 0; i < page.Count; i++)
        {
            if (i % config.Columns != 0)
                ImGui.SameLine();
            DrawTile(page[i], new Vector2(layout.TileWidth, layout.TileHeight));
        }
        DrawPager(gridTopLeft, layout);

        if (ImGui.IsWindowHovered() && ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
        {
            if (wheel < 0)
                state.NextPage();
            else
                state.PreviousPage();
        }
    }

    /// <summary>Page arrows and number, centered right below the full grid (also when the last page has fewer tiles).</summary>
    private void DrawPager(Vector2 gridTopLeft, PageLayoutResult layout)
    {
        var style = ImGui.GetStyle();
        var gridWidth = config.Columns * layout.TileWidth + (config.Columns - 1) * style.ItemSpacing.X;
        var gridHeight = config.Rows * layout.TileHeight + (config.Rows - 1) * style.ItemSpacing.Y;
        var pageText = $"Seite {state.PageIndex + 1} / {state.PageCount}";
        var arrow = ImGui.GetFrameHeight();
        var width = arrow * 2 + ImGui.CalcTextSize(pageText).X + style.ItemSpacing.X * 2;
        ImGui.SetCursorPos(gridTopLeft + new Vector2(MathF.Max(0, (gridWidth - width) / 2), gridHeight + style.ItemSpacing.Y));
        if (ImGui.ArrowButton("##prev", ImGuiDir.Left))
            state.PreviousPage();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(pageText);
        ImGui.SameLine();
        if (ImGui.ArrowButton("##next", ImGuiDir.Right))
            state.NextPage();
    }

    private const string WeaponsLabel = "Waffen gezogen";
    private const string DimLabel = "Abdunkeln";

    private static float CheckboxWidth(string label) => ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label).X;

    private static float DisplayOptionsWidth()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return CheckboxWidth(WeaponsLabel) + ImGui.GetFrameHeight() + CheckboxWidth(DimLabel) + spacing * 4;
    }

    /// <summary>Checkboxes right of the slot bar: drawn weapons (slower, with a warning) and dimming of missing items.</summary>
    private void DrawDisplayOptions()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - DisplayOptionsWidth() + spacing);
        var frameOffset = (ImGui.GetItemRectSize().Y - ImGui.GetFrameHeight()) / 2;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0, frameOffset));

        // All three items share the checkbox row's top; the (taller) warning glyph would otherwise push the second checkbox up.
        var rowY = ImGui.GetCursorPosY();
        var drawn = config.TileWeaponsDrawn;
        if (ImGui.Checkbox(WeaponsLabel, ref drawn))
            state.SetTileWeaponsDrawn(drawn);
        ImGui.SameLine(0, 2);
        ImGui.SetCursorPosY(rowY + ImGui.GetStyle().FramePadding.Y);
        using (UiScale.PushIcons())
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), FontAwesomeIcon.ExclamationTriangle.ToIconString());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Waffen-Kacheln mit gezogener Waffe: jede Kachel spielt das Ziehen ab, das Rendern dauert deutlich länger.");

        ImGui.SameLine();
        ImGui.SetCursorPosY(rowY);
        var dim = config.DimMissingTiles;
        if (ImGui.Checkbox(DimLabel, ref dim))
        {
            config.DimMissingTiles = dim;
            config.Save();
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
            drawList.AddImageRounded(texture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, TileStyle.ImageTint(owned, selected, config.DimMissingTiles), rounding);
        }
        else
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            var iconSize = new Vector2(MathF.Min(size.X * 0.5f, 64 * UiScale.Global));
            var iconPos = pos + (size - iconSize) / 2;
            drawList.AddImage(icon.Handle, iconPos, iconPos + iconSize);
        }

        // Name, level/source line and trait icons on a dark strip along the bottom edge.
        // A rounded strip (a gradient cannot be rounded and would stick out at the corners).
        // Short tiles drop the icon row first, then line 2; everything is clipped to the tile.
        var lineHeight = ImGui.GetTextLineHeight();
        var rows = TileInfo.InfoRows(size.Y, lineHeight);
        drawList.PushClipRect(pos, pos + size, true);
        if (rows > 0)
        {
            var iconSize = lineHeight * TraitIconScale;
            var stripHeight = rows == 3 ? lineHeight * 2.2f + iconSize + 10 : lineHeight * (rows + 0.2f) + 4 + rows;
            var shadeTop = pos + new Vector2(0, size.Y - stripHeight);
            drawList.AddRectFilled(shadeTop, pos + size, 0xB0000000, rounding, ImDrawFlags.RoundCornersBottom);
            var textX = 6f;
            var categories = sources.IsReady ? sources.Index.KnownCategoriesOf(item.Id) : null;
            drawList.AddText(shadeTop + new Vector2(textX, 4), colors.Text, Truncate(item.Name, size.X - 12));
            if (rows >= 2)
            {
                // Owned: where it is; missing: where to get it.
                var places = owned ? ownership.Current?.LocationsOf(item.Id).Select(l => l.DisplayName).ToList() ?? [] : [];
                var line = places.Count > 0
                    ? TileInfo.OwnedLine(item.LevelEquip, places)
                    : TileInfo.Line(item.LevelEquip, categories != null ? SourceRanking.Top(categories) : null);
                drawList.AddText(shadeTop + new Vector2(textX, 4 + lineHeight), colors.TextMuted, Truncate(line, size.X - 12));
            }
            if (rows >= 3)
                DrawTraitIcons(drawList, item, categories, shadeTop + new Vector2(textX, 6 + lineHeight * 2), size.X - 12, iconSize);
        }

        if (pool.Failures.TryGetValue(key, out var error))
        {
            drawList.AddText(pos + new Vector2(6, 4), ErrorColor, "!");
            var labelPos = pos + new Vector2(6, 4 + lineHeight);
            var label = Truncate("Renderfehler", size.X - 12);
            drawList.AddText(labelPos, ErrorColor, label);
            // Like the trait icons, only the marker itself shows a tooltip: the reason of the failure.
            if (hovered && ImGui.IsMouseHoveringRect(pos, labelPos + ImGui.CalcTextSize(label)))
                ImGui.SetTooltip($"Renderfehler: {error}");
        }
        drawList.PopClipRect();

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
        // Tooltips are built only for the hovered icon.
        var icons = new List<(Action<Vector2> Draw, Func<string> Tooltip)>();
        if (item.DyeCount > 0)
            icons.Add((at =>
            {
                GameIcon.DrawAt(drawList, TraitIcons.Dye, at, new Vector2(iconSize), FontAwesomeIcon.PaintBrush, colors.TextMuted);
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, at + new Vector2(iconSize * 0.6f, iconSize * 0.35f), colors.Text, item.DyeCount.ToString());
            }, () => TileInfo.DyeText(item.DyeCount)));
        if (item.Trade != Tradability.Bound)
        {
            var (tradeIcon, glyph) = item.Trade == Tradability.Marketable
                ? (TraitIcons.Marketable, FontAwesomeIcon.Store)
                : (TraitIcons.Tradable, FontAwesomeIcon.ExchangeAlt);
            icons.Add((at => GameIcon.DrawAt(drawList, tradeIcon, at, new Vector2(iconSize), glyph, colors.TextMuted), () => TileInfo.TradeText(item.Trade)));
        }
        // The job/role icon stands apart, right-aligned.
        (Action<Vector2> Draw, Func<string> Tooltip)? right = null;
        var badge = JobBadges.For(JobBadges.CollapseClasses(item.ClassJobIds, jobs.ParentOf), jobs.GroupOf, jobs.All.Count);
        if (badge.Kind != JobBadgeKind.None)
        {
            var job = badge.Kind == JobBadgeKind.Job ? jobs.Find(badge.JobId) : null;
            var icon = job?.IconId ?? JobCatalog.RoleIcon(badge.Group);
            var name = job?.Name ?? JobGroups.DisplayName(badge.Group);
            right = (at => GameIcon.DrawAt(drawList, icon, at, new Vector2(iconSize), FontAwesomeIcon.User, colors.TextMuted), () => name);
        }
        if (categories != null)
        {
            foreach (var category in SourceRanking.Icons(categories))
                icons.Add((at => DrawSourceIcon(drawList, category, at, iconSize, colors.TextMuted),
                           () => TileInfo.SourceTooltip(category, sources.Index.SourcesOf(item.Id))));
        }

        const float spacing = 3f;
        var (fit, showRight) = TileInfo.FitRow(icons.Count, right != null, iconSize, spacing, width);
        var windowHovered = ImGui.IsWindowHovered();
        void Place((Action<Vector2> Draw, Func<string> Tooltip) icon, Vector2 at)
        {
            icon.Draw(at);
            // Only the icon itself shows a tooltip; the rest of the tile has none.
            if (windowHovered && ImGui.IsMouseHoveringRect(at, at + new Vector2(iconSize)))
                ImGui.SetTooltip(icon.Tooltip());
        }
        for (var i = 0; i < fit; i++)
            Place(icons[i], min + new Vector2(i * (iconSize + spacing), 0));
        if (showRight)
            Place(right!.Value, min + new Vector2(width - iconSize, 0));
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
