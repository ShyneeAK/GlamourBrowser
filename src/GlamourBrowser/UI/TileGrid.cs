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
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Wishlist;

namespace GlamourBrowser.UI;

/// <summary>
/// The tile grid: rounded cards with name, level/source line and trait icons inside the image, plus ownership frame/badge
/// and the wishlist star; page controls below. In the wishlist mode it can show the plan by source instead.
/// </summary>
public sealed class TileGrid
{
    private const uint ErrorColor = 0xFF4040E0;
    // Trait and source icons at the bottom of a tile, relative to the line height.
    private const float TraitIconScale = 1.5f;
    // Upper bound for cached tile texts before they are dropped and rebuilt for the shown pages.
    private const int MaxLabels = 1000;

    private enum TraitKind { Dye, Trade, Source, Job }

    /// <param name="Label">Text drawn over the icon (the dye channel count).</param>
    /// <param name="Tooltip">Fixed tooltip; source icons build theirs only while hovered.</param>
    private readonly record struct TraitIcon(TraitKind Kind, uint IconId, FontAwesomeIcon Fallback, SourceCategory Category = default,
                                             string? Label = null, string? Tooltip = null);

    /// <summary>A tile's texts, already cut to the tile width, and its icons: built once per item, not every frame.</summary>
    private sealed record TileLabels(string Name, string Line, TraitIcon[] Icons, TraitIcon? Job);

    private readonly BrowserState state;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly RenderPool pool;
    private readonly OwnershipService ownership;
    private readonly LivePreview live;
    private readonly Configuration config;
    private readonly Theme.Theme theme;
    private readonly SourceService sources;
    private readonly JobCatalog jobs;
    private readonly WishlistService wishlist;
    private readonly WishlistPlanView planView;
    private readonly Dictionary<(uint ItemId, SetId Set), TileLabels> labels = new();
    private (float Width, float FontSize, int Ownership, bool SourcesReady) labelsFor;
    private int ownershipVersion;

    public TileGrid(BrowserState state, TileCache<IDalamudTextureWrap> cache, RenderPool pool, OwnershipService ownership,
                    LivePreview live, Configuration config, Theme.Theme theme, SourceService sources, JobCatalog jobs,
                    WishlistService wishlist, WishlistPlanView planView)
    {
        this.sources = sources;
        this.wishlist = wishlist;
        this.planView = planView;
        this.jobs = jobs;
        this.theme = theme;
        this.state = state;
        this.cache = cache;
        this.pool = pool;
        this.ownership = ownership;
        this.live = live;
        this.config = config;
        ownership.Changed += () => ownershipVersion++;
    }

    public void Draw()
    {
        // Slot selection sits above the tiles it filters, the tile display options right-aligned next to it.
        var filter = state.Filter;
        if (SlotBar.Draw("slotbar", ref filter, theme, DisplayOptionsWidth()))
            state.Filter = filter;
        DrawDisplayOptions();
        ImGui.Spacing();

        // Logged out the render pool pauses too; the wishlist mode says why it is empty.
        if (state.Filter.Wishlist && wishlist.Current == null)
        {
            ImGui.TextDisabled(wishlist.LoadFailed ? "Wunschliste konnte nicht geladen werden" : "Nicht eingeloggt");
            return;
        }

        if (pool.IsPaused)
        {
            ImGui.TextDisabled("Pausiert");
            return;
        }

        if (pool.Status is { } status)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), status);

        if (state.Filter.Wishlist && !DrawWishlistHeader())
            return;

        if (state.Filter.Sets && state.Results.Count == 0)
        {
            if (!sources.IsReady)
            {
                ImGui.TextDisabled("Ausrüstungs-Sets werden geladen …");
                return;
            }
            if (sources.Index.Failures.Contains(SourceIndexBuilder.SetsFamily))
            {
                ImGui.TextDisabled("Ausrüstungs-Sets konnten nicht geladen werden");
                return;
            }
        }

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

        // Tile texts depend on the width, the font, ownership and the source index: rebuilt when one of them changes.
        var labelsKey = (layout.TileWidth, ImGui.GetFontSize(), ownershipVersion, sources.IsReady);
        if (labelsKey != labelsFor || labels.Count > MaxLabels)
        {
            labels.Clear();
            labelsFor = labelsKey;
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

    /// <summary>The wishlist mode's hints, view switch and filter (tiles only), or the plan by source; false when no grid follows.</summary>
    private bool DrawWishlistHeader()
    {
        if (wishlist.Current is not { } store)
        {
            ImGui.TextDisabled(wishlist.LoadFailed ? "Wunschliste konnte nicht geladen werden" : "Nicht eingeloggt");
            return false;
        }
        if (store.Entries.Count == 0)
        {
            ImGui.TextDisabled("Noch nichts auf der Wunschliste – Stern auf einer Kachel oder in der Item-Karte setzen");
            return false;
        }

        var bySource = config.WishlistBySource;
        if (ImGui.RadioButton("Kacheln", !bySource) && bySource)
            state.SetWishlistBySource(false);
        ImGui.SameLine();
        if (ImGui.RadioButton("Nach Quelle", bySource) && !bySource)
            state.SetWishlistBySource(true);
        // The plan lists only missing pieces anyway.
        if (!bySource)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 4);
            var hideDone = config.HideDoneWishes;
            if (ImGui.Checkbox("Erledigte ausblenden", ref hideDone))
                state.SetHideDoneWishes(hideDone);
        }

        // Set entries resolve only once the source index is there (the plan says it loads on its own).
        if (!bySource && state.WishlistEntries.Count < store.Entries.Count && !sources.IsReady)
            ImGui.TextDisabled("Ausrüstungs-Sets werden geladen …");
        if (bySource)
        {
            planView.Draw();
            return false;
        }

        if (WishResults.EmptyHint(state.WishlistEntries.Count, state.Results.Count, state.Filter.SearchText, config.HideDoneWishes, !sources.IsReady) is { } hint)
        {
            ImGui.TextDisabled(hint);
            return false;
        }
        return state.Results.Count > 0;
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

    private void DrawTile(TileSubject subject, Vector2 size)
    {
        const float rounding = 8f;
        var colors = theme.Colors;
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##tile{subject.Id.ItemId}-{subject.Id.Set.Kind}{subject.Id.Set.Value}", size);
        var hovered = ImGui.IsItemHovered();
        // The star in the top-left corner puts the item or set on the wishlist; a click anywhere else selects the tile.
        var starPos = pos + new Vector2(6, 5);
        var starSize = WishStarSize();
        var canWish = wishlist.Current != null;
        var (starMin, starMax) = (starPos - new Vector2(3), starPos + starSize + new Vector2(3));
        var overStar = hovered && canWish && ImGui.IsMouseHoveringRect(starMin, starMax);
        // Decided where the button went down: a press on the star toggles the wish wherever it is released on the tile.
        var pressed = ImGui.GetIO().MouseClickedPos[0];
        var pressedOnStar = canWish && pressed.X >= starMin.X && pressed.Y >= starMin.Y && pressed.X <= starMax.X && pressed.Y <= starMax.Y;
        if (clicked && pressedOnStar)
            wishlist.Toggle(subject);
        else if (clicked)
        {
            state.Selected = subject;
            live.Select(subject);
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, hovered ? colors.CardRaised : colors.Card, rounding);

        var key = state.KeyFor(subject);
        // A set counts as owned when it is complete.
        var owned = subject is SetSubject set ? SetProgress.Of(set, IsOwned).Complete : IsOwned(subject.Wear[0].Id);
        var selected = state.Selected?.Id == subject.Id;
        if (cache.TryGet(key, out var texture) && texture != null)
        {
            drawList.AddImageRounded(texture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, TileStyle.ImageTint(owned, selected, config.DimMissingTiles), rounding);
        }
        else
        {
            // A set shows the icon of its body piece (or its first piece) until the render is there.
            var iconId = (subject.Wear.FirstOrDefault(w => w.Slot == EquipSlot.Body) ?? subject.Wear[0]).IconId;
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
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
            var text = LabelsFor(subject, owned, size.X - 12);
            drawList.AddText(shadeTop + new Vector2(textX, 4), colors.Text, text.Name);
            if (rows >= 2)
                drawList.AddText(shadeTop + new Vector2(textX, 4 + lineHeight), colors.TextMuted, text.Line);
            if (rows >= 3)
                DrawTraitIcons(drawList, subject, text, shadeTop + new Vector2(textX, 6 + lineHeight * 2), size.X - 12, iconSize);
        }

        // Amber when listed, faint on a hovered tile to show where to click; a render failure's marker moves right of it.
        var listed = wishlist.IsListed(subject);
        var showStar = listed || hovered && canWish;
        if (showStar)
        {
            DrawWishStar(drawList, starPos, listed ? colors.Warning : overStar ? colors.Text : (colors.TextMuted & 0x00FFFFFF) | 0xA0000000);
            if (overStar)
                ImGui.SetTooltip(listed ? "Von der Wunschliste nehmen" : "Auf die Wunschliste");
        }
        // The star's place stays reserved while it can show, so the marker does not jump when the tile is hovered.
        var markerX = showStar || canWish ? 6 + starSize.X + 3 : 6f;
        if (pool.Failures.TryGetValue(key, out var error))
        {
            drawList.AddText(pos + new Vector2(markerX, 4), ErrorColor, "!");
            var labelPos = pos + new Vector2(6, 4 + lineHeight);
            var label = Truncate("Renderfehler", size.X - 12);
            drawList.AddText(labelPos, ErrorColor, label);
            // Like the trait icons, only the marker itself shows a tooltip: the reason of the failure.
            if (hovered && !overStar && ImGui.IsMouseHoveringRect(pos, labelPos + ImGui.CalcTextSize(label)))
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

    private bool IsOwned(uint itemId) => ownership.Current?.IsOwned(itemId) ?? false;

    /// <summary>The tile's wishlist star with a dark shadow, readable on bright renders too.</summary>
    private static void DrawWishStar(ImDrawListPtr drawList, Vector2 at, uint color)
    {
        using (UiScale.PushIcons())
        {
            var glyph = FontAwesomeIcon.Star.ToIconString();
            drawList.AddText(at + Vector2.One, 0xC0000000, glyph);
            drawList.AddText(at, color, glyph);
        }
    }

    private static Vector2 WishStarSize()
    {
        using (UiScale.PushIcons())
            return ImGui.CalcTextSize(FontAwesomeIcon.Star.ToIconString());
    }

    /// <summary>The tile's texts and icons from the per-subject cache, built on the first frame the tile shows.</summary>
    private TileLabels LabelsFor(TileSubject subject, bool owned, float width)
    {
        if (labels.TryGetValue(subject.Id, out var cached))
            return cached;
        return labels[subject.Id] = subject switch
        {
            SetSubject set => SetLabels(set, width),
            ItemSubject { Item: var item } => ItemLabels(item, owned, width),
            _ => throw new ArgumentOutOfRangeException(nameof(subject)),
        };
    }

    private TileLabels ItemLabels(GlamourItem item, bool owned, float width)
    {
        var categories = sources.IsReady ? sources.Index.KnownCategoriesOf(item.Id) : null;
        // Owned: where it is; missing: where to get it.
        var places = owned ? ownership.Current?.LocationsOf(item.Id).Select(l => l.DisplayName).ToList() ?? [] : [];
        var line = places.Count > 0
            ? TileInfo.OwnedLine(item.LevelEquip, places)
            : TileInfo.Line(item.LevelEquip, categories != null ? SourceRanking.Top(categories) : null);

        var icons = new List<TraitIcon>();
        if (item.DyeCount > 0)
            icons.Add(new TraitIcon(TraitKind.Dye, TraitIcons.Dye, FontAwesomeIcon.PaintBrush, Label: item.DyeCount.ToString(), Tooltip: TileInfo.DyeText(item.DyeCount)));
        if (item.Trade != Tradability.Bound)
        {
            var (tradeIcon, glyph) = item.Trade == Tradability.Marketable
                ? (TraitIcons.Marketable, FontAwesomeIcon.Store)
                : (TraitIcons.Tradable, FontAwesomeIcon.ExchangeAlt);
            icons.Add(new TraitIcon(TraitKind.Trade, tradeIcon, glyph, Tooltip: TileInfo.TradeText(item.Trade)));
        }
        if (categories != null)
            icons.AddRange(SourceTraitIcons(categories));
        return new TileLabels(Truncate(item.Name, width), Truncate(line, width), icons.ToArray(), JobIcon(item.ClassJobIds));
    }

    /// <summary>A set: name, level and progress, the source icons of all pieces and the jobs every armour piece fits.</summary>
    private TileLabels SetLabels(SetSubject set, float width)
    {
        var categories = sources.IsReady ? SetFilter.CategoriesOf(set, sources.Index.KnownCategoriesOf) : null;
        var line = TileInfo.SetLine(set.Set.Level, SetProgress.Of(set, IsOwned));
        return new TileLabels(Truncate(set.Name, width), Truncate(line, width), categories != null ? SourceTraitIcons(categories).ToArray() : [], JobIcon(set.ArmourJobs));
    }

    private static IEnumerable<TraitIcon> SourceTraitIcons(IReadOnlySet<SourceCategory> categories)
        => SourceRanking.Icons(categories).Select(category => new TraitIcon(TraitKind.Source, 0, default, category));

    /// <summary>The job/role icon; it stands apart, right-aligned.</summary>
    private TraitIcon? JobIcon(IReadOnlySet<uint> classJobIds)
    {
        var badge = JobBadges.For(JobBadges.CollapseClasses(classJobIds, jobs.ParentOf), jobs.GroupOf, jobs.All.Count);
        if (badge.Kind == JobBadgeKind.None)
            return null;
        var info = badge.Kind == JobBadgeKind.Job ? jobs.Find(badge.JobId) : null;
        return new TraitIcon(TraitKind.Job, info?.IconId ?? JobCatalog.RoleIcon(badge.Group), FontAwesomeIcon.User,
                             Tooltip: info?.Name ?? JobGroups.DisplayName(badge.Group));
    }

    /// <summary>Dye channels, tradability and one source icon per group, the job/role right-aligned; cut off on the right when the tile is narrow.</summary>
    private void DrawTraitIcons(ImDrawListPtr drawList, TileSubject subject, TileLabels text, Vector2 min, float width, float iconSize)
    {
        const float spacing = 3f;
        var (fit, showRight) = TileInfo.FitRow(text.Icons.Length, text.Job != null, iconSize, spacing, width);
        var windowHovered = ImGui.IsWindowHovered();
        for (var i = 0; i < fit; i++)
            DrawTrait(drawList, subject, text.Icons[i], min + new Vector2(i * (iconSize + spacing), 0), iconSize, windowHovered);
        if (showRight)
            DrawTrait(drawList, subject, text.Job!.Value, min + new Vector2(width - iconSize, 0), iconSize, windowHovered);
    }

    private void DrawTrait(ImDrawListPtr drawList, TileSubject subject, TraitIcon icon, Vector2 at, float iconSize, bool windowHovered)
    {
        var colors = theme.Colors;
        if (icon.Kind == TraitKind.Source)
            DrawSourceIcon(drawList, icon.Category, at, iconSize, colors.TextMuted);
        else
            GameIcon.DrawAt(drawList, icon.IconId, at, new Vector2(iconSize), icon.Fallback, colors.TextMuted);
        if (icon.Label != null)
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, at + new Vector2(iconSize * 0.6f, iconSize * 0.35f), colors.Text, icon.Label);
        // Only the icon itself shows a tooltip; the rest of the tile has none.
        if (windowHovered && ImGui.IsMouseHoveringRect(at, at + new Vector2(iconSize)))
            ImGui.SetTooltip(icon.Tooltip ?? SourceTooltip(subject, icon.Category));
    }

    private string SourceTooltip(TileSubject subject, SourceCategory category) => subject is SetSubject set
        ? TileInfo.SetSourceTooltip(category, set.Pieces.Select(p => (p.Name, sources.Index.SourcesOf(p.Id))).ToList())
        : TileInfo.SourceTooltip(category, sources.Index.SourcesOf(subject.Wear[0].Id));

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
