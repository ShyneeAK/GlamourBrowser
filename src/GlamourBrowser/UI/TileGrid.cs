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
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Core.CustomSets;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Market;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;
using GlamourBrowser.GlamourPlate;

namespace GlamourBrowser.UI;

/// <summary>
/// The tile grid: rounded cards with name, level/source line and trait icons inside the image, plus ownership frame/badge
/// and the wishlist star; page controls below. In the wishlist mode it can show the plan by source instead.
/// </summary>
public sealed class TileGrid
{
    private const uint ErrorColor = 0xFF4040E0;
    // The heart of a favourite: a warm pink, apart from the amber of the wishlist star.
    private const uint FavoriteColor = 0xFF8C5AF0;
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
    private readonly CharacterListService wishlist;
    private readonly WishlistPlanView planView;
    private readonly CustomSetService customSets;
    private readonly CustomSetEditor editor;
    private readonly GlamourPlateBridge plates;
    private readonly MemoryStats memory;
    private readonly TryOnService tryOn;
    private readonly TileFilterControls filterControls;
    // What the last import did, shown next to its button for a moment.
    private (string Text, DateTime At)? importStatus;
    private const string ContextPopup = "##tilecontext";
    // The tile whose context menu is open.
    private TileSubject? contextSubject;
    private const string DeletePopup = "##tiledelete";
    private CustomSet? deleteSet;
    // A custom set keeps its id while it changes: its content hash and name are part of the key.
    private readonly Dictionary<((uint ItemId, SetId Set) Id, uint Variant, string Name), TileLabels> labels = new();
    private (float Width, float FontSize, int Ownership, int Sources, int Language) labelsFor;
    private int ownershipVersion;
    private readonly WheelSteps wheel = new();
    private readonly DyeCatalog dyes;
    private const string TileDyePopup = "##tiledye";
    // The item tile and dye channel (0 or 1) whose box opened the dye popup.
    private (GlamourItem Item, int Channel)? dyeing;
    private readonly MySetsFolders folders;
    private readonly MarketService market;

    public TileGrid(BrowserState state, TileCache<IDalamudTextureWrap> cache, RenderPool pool, OwnershipService ownership,
                    LivePreview live, Configuration config, Theme.Theme theme, SourceService sources, JobCatalog jobs,
                    CharacterListService wishlist, WishlistPlanView planView, CustomSetService customSets, CustomSetEditor editor, GlamourPlateBridge plates,
                    MemoryStats memory, TryOnService tryOn, TileFilterControls filterControls, MarketService market, DyeCatalog dyes)
    {
        this.dyes = dyes;
        this.market = market;
        this.tryOn = tryOn;
        this.filterControls = filterControls;
        this.memory = memory;
        this.sources = sources;
        this.wishlist = wishlist;
        this.planView = planView;
        this.customSets = customSets;
        this.editor = editor;
        this.plates = plates;
        this.jobs = jobs;
        this.theme = theme;
        this.state = state;
        this.cache = cache;
        this.pool = pool;
        this.ownership = ownership;
        this.live = live;
        this.config = config;
        folders = new MySetsFolders(state, customSets, cache, theme);
        ownership.Changed += () => ownershipVersion++;
    }

    public void Draw()
    {
        // Slot selection sits above the tiles it filters, the tile display options right-aligned next to it; the modes
        // ("Meine Sets", "Wunschliste", "Zuletzt benutzt", "Favoriten"), the tile dye and the jobs in a second row below.
        var filter = state.Filter;
        var edge = SlotBar.Edge(DisplayOptionsWidth());
        var changed = SlotBar.Draw("slotbar", ref filter, theme, edge);
        DrawDisplayOptions();
        changed |= SlotBar.DrawModes("slotbar", ref filter, theme, edge);
        if (changed)
        {
            state.Filter = filter;
            // A selection of another slot goes: the camera button would otherwise calibrate that slot. The set editor
            // shows its draft as the selection and keeps it.
            if (!editor.IsOpen && !SlotSelection.KeepsSelection(state.Selected, filter))
            {
                state.Selected = null;
                live.Select(null);
            }
        }
        DrawFilterControls(edge);
        ImGui.Spacing();

        // Logged out the render pool pauses too; the wishlist mode says why it is empty.
        if (state.Filter.Mode == BrowseMode.Wishlist && wishlist.Current == null)
        {
            ImGui.TextDisabled(wishlist.LoadFailed ? Loc.T("Wunschliste konnte nicht geladen werden") : Loc.T("Nicht eingeloggt"));
            return;
        }
        // An opened model or set stays when the list empties meanwhile (its last favourite taken off on a twin's tile).
        if (state.Filter.Mode == BrowseMode.History && !state.PiecesOpen && !DrawItemListState(state.History, Loc.T("Noch keine Items angeklickt")))
            return;
        if (state.Filter.Mode == BrowseMode.Favorites && !state.PiecesOpen
            && !DrawItemListState(state.Favorites, Loc.T("Noch keine Favoriten – das Herz oben links auf einer Kachel merkt ein Item vor")))
            return;

        if (pool.IsPaused)
        {
            ImGui.TextDisabled(Plugin.PauseReason is { } reason ? Loc.F("Pausiert: {0}", reason) : Loc.T("Pausiert"));
            return;
        }

        if (pool.Status is { } status)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), status);

        if (state.OpenSets is { } openSets)
            DrawPiecesHeader(SlotIcons.Sets, FontAwesomeIcon.LayerGroup, string.Join(" · ", openSets.Select(s => s.Name)));
        else if (state.OpenModel is { } model)
            DrawPiecesHeader(0, FontAwesomeIcon.Cubes, Loc.F("Gleiches Modell wie „{0}“", model.Name));
        else if (state.Filter.Mode == BrowseMode.MySets && customSets.Current == null)
        {
            ImGui.TextDisabled(customSets.LoadFailed ? Loc.T("Eigene Sets konnten nicht geladen werden") : Loc.T("Nicht eingeloggt"));
            return;
        }
        else if (state.Filter.Mode == BrowseMode.MySets && !DrawMySetsHeader())
            return;
        else if (state.Filter.Mode == BrowseMode.Wishlist && !DrawWishlistHeader())
            return;
        else if (BrowseModes.ItemList(state.Filter.Mode) && DrawListBar(state.Filter.Mode))
        {
            // The filters apply to the plan too: with none left it would say "Alles erledigt".
            if (state.Results.Count == 0)
                DrawNoResults();
            else
                planView.Draw(editor, state.Results);
            return;
        }
        else if (state.Filter.Mode == BrowseMode.Sets && state.Results.Count == 0)
        {
            if (!sources.IsReady)
            {
                ImGui.TextDisabled(Loc.T("Ausrüstungs-Sets werden geladen …"));
                return;
            }
            if (sources.Index.Failures.Contains(SourceIndexBuilder.SetsFamily))
            {
                ImGui.TextDisabled(Loc.T("Ausrüstungs-Sets konnten nicht geladen werden"));
                return;
            }
        }
        // Nothing found instead of an empty grid, with a way out; not while the ownership filter waits for the ownership
        // data (at login), where a click would throw away the restored filter.
        var waitsForOwnership = state.Filter.Ownership != OwnershipFilter.All && ownership.Current == null;
        if (!state.PiecesOpen && state.Filter.Mode is BrowseMode.Items or BrowseMode.Sets or BrowseMode.History or BrowseMode.Favorites
            && state.Results.Count == 0 && !waitsForOwnership)
        {
            DrawNoResults();
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        // Names are drawn inside the tile, so no extra label row is needed; the page controls below take one row.
        var pagerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
        // The debug memory line takes one more row below the page controls.
        if (config.ShowMemoryStats)
            pagerHeight += ImGui.GetTextLineHeightWithSpacing();
        state.UpdateLayout(avail.X, avail.Y - pagerHeight, spacing, 0);

        var layout = state.Layout;
        if (layout.TileWidth <= 0)
        {
            ImGui.TextDisabled(Loc.T("Fenster vergrößern oder weniger Kacheln wählen"));
            return;
        }

        // Tile texts depend on the width, the font, ownership, the source index and the language: rebuilt when one of them changes.
        var labelsKey = (layout.TileWidth, ImGui.GetFontSize(), ownershipVersion, sources.Version, Loc.Version);
        if (labelsKey != labelsFor || labels.Count > MaxLabels)
        {
            labels.Clear();
            labelsFor = labelsKey;
        }

        var gridTopLeft = ImGui.GetCursorPos();
        var gridScreen = ImGui.GetCursorScreenPos();
        var page = state.CurrentPage;
        for (var i = 0; i < page.Count; i++)
        {
            if (i % config.Columns != 0)
                ImGui.SameLine();
            DrawTile(page[i], new Vector2(layout.TileWidth, layout.TileHeight));
        }
        DrawContextMenu();
        DrawTileDyePopup();
        DrawDeleteConfirm();
        // The page controls sit in the middle of the room left under the tiles (above the memory line).
        var pagerRoom = avail.Y - (config.ShowMemoryStats ? ImGui.GetTextLineHeightWithSpacing() : 0);
        DrawPager(gridTopLeft, layout, pagerRoom);

        // The wheel pages over the tiles and the pager only (not over the slot bar above); Shift: ten pages at a time.
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(gridScreen, gridScreen + new Vector2(avail.X, pagerRoom)))
        {
            // Some systems turn Shift+wheel into the horizontal wheel.
            var io = ImGui.GetIO();
            var steps = wheel.Add(io.KeyShift && io.MouseWheel == 0 ? io.MouseWheelH : io.MouseWheel);
            if (steps != 0)
                state.MoveBy(-steps * (io.KeyShift ? 10 : 1));
        }
        else
            wheel.Reset();
        HandlePageKeys();
    }

    /// <summary>
    /// Above the pieces of the sets opened from the item card, or the items of one look opened from a tile: back to the grid
    /// before, and what is shown.
    /// </summary>
    private void DrawPiecesHeader(UiIcon icon, FontAwesomeIcon fallback, string title)
    {
        if (ImGui.Button("← " + Loc.T("Zurück")))
            state.CloseSetPieces();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Zurück zur vorherigen Ansicht"));
        ImGui.SameLine();
        GameIcon.Draw(icon, new Vector2(ImGui.GetFrameHeight()), fallback, theme.Colors.TextMuted);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(title);
    }

    /// <summary>The custom set mode's "+ Neues Set" with its start points, and its hints; false when no grid follows.</summary>
    private bool DrawMySetsHeader()
    {
        if (customSets.Current is not { } store)
            return false;

        folders.DrawHeader(store);
        if (ImGui.Button(Loc.T("+ Neues Set")))
            ImGui.OpenPopup("##newset");
        if (ImGui.BeginPopup("##newset"))
        {
            if (ImGui.Selectable(Loc.T("Leer")))
                editor.StartNew(CustomSetStore.DefaultName, []);
            if (ImGui.Selectable(Loc.T("Aktuelles Outfit")))
                editor.StartFromOutfit();
            DrawPlateStart();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!plates.PlatesLoaded))
        {
            if (ImGui.Button(Loc.T("Alle Glamour-Plates importieren")))
            {
                var (imported, skipped) = plates.ImportAll(store, IsKnown);
                importStatus = (Loc.F("{0} Sets importiert, {1} übersprungen", imported, skipped), DateTime.Now);
            }
        }
        if (!plates.PlatesLoaded && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Glamour-Plates zuerst an der Kommode öffnen"));
        if (importStatus is { } status && DateTime.Now - status.At < TimeSpan.FromSeconds(5))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(status.Text);
        }
        folders.DrawMarks(store);

        folders.DrawPopups();

        if (store.Sets.Count == 0 && store.Folders.Count == 0)
        {
            ImGui.TextDisabled(Loc.T("Noch keine eigenen Sets – „+ Neues Set“ anklicken"));
            return false;
        }
        if (state.Results.Count == 0 && state.OpenFolder != 0 && state.Filter.SearchText.Trim().Length == 0)
        {
            ImGui.TextDisabled(Loc.T("Dieser Ordner ist leer – Sets per Rechtsklick „In Ordner verschieben“ hineinlegen oder in der Übersicht auf seine Kachel ziehen"));
            return false;
        }
        if (state.Results.Count == 0)
        {
            ImGui.TextDisabled(state.MySets.Count == 0 ? Loc.T("Keine Einträge im Katalog gefunden") : Loc.T("Keine Treffer"));
            return false;
        }
        return true;
    }

    private bool IsKnown(uint itemId) => state.ItemOf(itemId) != null;

    /// <summary>The start point "Glamour-Plate": one entry per saved plate with its number of pieces, once the game has the plates.</summary>
    private void DrawPlateStart()
    {
        if (!ImGui.BeginMenu(Loc.T("Glamour-Plate"), plates.PlatesLoaded))
        {
            if (!plates.PlatesLoaded && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(Loc.T("Glamour-Plates zuerst an der Kommode öffnen"));
            return;
        }
        for (var i = 0; i < plates.PlateCount; i++)
        {
            if (plates.ReadPlate(i, IsKnown) is not { } pieces)
                continue;
            if (ImGui.MenuItem(pieces.Count == 1 ? Loc.F("Plate {0} · 1 Teil", i + 1) : Loc.F("Plate {0} · {1} Teile", i + 1, pieces.Count)))
            {
                editor.StartNew(Loc.F("Glamour-Plate {0}", i + 1), pieces);
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndMenu();
    }

    /// <summary>"Keine Treffer" with a way out, instead of an empty grid.</summary>
    private void DrawNoResults()
    {
        ImGui.TextDisabled(Loc.T("Keine Treffer"));
        if (FilterChips.CanResetAll(state.Filter) && ImGui.Button(Loc.T("Alle zurücksetzen")))
            state.Filter = FilterChips.ResetAll(state.Filter);
    }

    /// <summary>Why "Zuletzt benutzt" or "Favoriten" shows no tiles while logged out or still empty; false when there are none to show.</summary>
    private static bool DrawItemListState(CharacterListService list, string emptyHint)
    {
        if (list.Current is not { } store)
        {
            ImGui.TextDisabled(list.LoadFailed ? Loc.T("Liste konnte nicht geladen werden") : Loc.T("Nicht eingeloggt"));
            return false;
        }
        if (store.Entries.Count > 0)
            return true;
        ImGui.TextDisabled(emptyHint);
        return false;
    }

    /// <summary>
    /// The bar of the wishlist, "Zuletzt benutzt" and the favourites: tiles or the missing pieces by source; with tiles
    /// "Erledigte ausblenden" (wishlist only) and whether the tiles wear the outfit. Returns whether the list shows by source.
    /// </summary>
    private bool DrawListBar(BrowseMode mode)
    {
        var bySource = state.BySource(mode);
        if (ImGui.RadioButton(Loc.T("Kacheln"), !bySource) && bySource)
            state.SetBySource(mode, false);
        ImGui.SameLine();
        if (ImGui.RadioButton(Loc.T("Nach Quelle"), bySource) && !bySource)
            state.SetBySource(mode, true);
        // The plan lists only missing pieces anyway, and shows no tiles.
        if (bySource)
            return true;

        if (mode == BrowseMode.Wishlist)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 4);
            var hideDone = config.HideDoneWishes;
            if (ImGui.Checkbox(Loc.T("Erledigte ausblenden"), ref hideDone))
                state.SetHideDoneWishes(hideDone);
        }
        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 4);
        var outfit = state.WearsOutfit(mode);
        if (ImGui.Checkbox(Loc.T("Outfit auf Kacheln"), ref outfit))
            state.SetWearsOutfit(mode, outfit);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Die Kacheln tragen das Outfit der Live-Ansicht; aus zeigen sie nur das Item bzw. Set selbst"));
        return false;
    }

    /// <summary>The wishlist mode's hints, view switch and filter (tiles only), or the plan by source; false when no grid follows.</summary>
    private bool DrawWishlistHeader()
    {
        if (wishlist.Current is not { } store)
        {
            ImGui.TextDisabled(wishlist.LoadFailed ? Loc.T("Wunschliste konnte nicht geladen werden") : Loc.T("Nicht eingeloggt"));
            return false;
        }
        if (store.Entries.Count == 0)
        {
            ImGui.TextDisabled(Loc.T("Noch nichts auf der Wunschliste – Stern auf einer Kachel oder in der Item-Karte setzen"));
            return false;
        }

        var bySource = DrawListBar(BrowseMode.Wishlist);

        // Set entries resolve only once the source index is there (the plan says it loads on its own).
        if (!bySource && state.WishlistEntries.Count < store.Entries.Count && !sources.IsReady)
            ImGui.TextDisabled(Loc.T("Ausrüstungs-Sets werden geladen …"));
        if (bySource)
        {
            planView.Draw(editor, state.WishlistEntries);
            return false;
        }

        if (WishResults.EmptyHint(state.WishlistEntries.Count, state.Results.Count, state.Filter.SearchText, config.HideDoneWishes, !sources.IsReady) is { } hint)
        {
            ImGui.TextDisabled(hint);
            return false;
        }
        return state.Results.Count > 0;
    }

    /// <summary>
    /// First page, page arrows, number and last page, centered right below the full grid (also when the last page has
    /// fewer tiles).
    /// </summary>
    /// <param name="room">Height from the grid's top to the bottom of the room for the page controls.</param>
    private const string PagePopup = "##gotopage";
    // The page number being typed (1 for the first), taken on Enter.
    private int typedPage;

    private void DrawPagePopup()
    {
        if (!ImGui.BeginPopup(PagePopup))
            return;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(110 * UiScale.Global);
        // No +/- buttons (they would count as Enter); the id stays when the page count changes while typing.
        if (ImGui.InputInt($"{Loc.F("von {0}", state.PageCount)}###gotopage", ref typedPage, 0, 0, flags: ImGuiInputTextFlags.EnterReturnsTrue))
        {
            state.ShowPage(typedPage - 1);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    /// <summary>
    /// Page keys while the main window has the focus and nothing is typed: Page Up/Down, Home and End. Not the arrow keys:
    /// the game gets the keys as well (ImGui does not take them without a text field), and it turns the camera with those.
    /// </summary>
    private void HandlePageKeys()
    {
        var io = ImGui.GetIO();
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || io.WantTextInput || ImGui.IsAnyItemActive()
            || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
            return;
        if (ImGui.IsKeyPressed(ImGuiKey.PageDown))
            state.NextPage();
        else if (ImGui.IsKeyPressed(ImGuiKey.PageUp))
            state.PreviousPage();
        else if (ImGui.IsKeyPressed(ImGuiKey.Home, false))
            state.ShowFirstPage();
        else if (ImGui.IsKeyPressed(ImGuiKey.End, false))
            state.ShowLastPage();
    }

    private void DrawPager(Vector2 gridTopLeft, PageLayoutResult layout, float room)
    {
        var style = ImGui.GetStyle();
        var gridWidth = config.Columns * layout.TileWidth + (config.Columns - 1) * style.ItemSpacing.X;
        var gridHeight = config.Rows * layout.TileHeight + (config.Rows - 1) * style.ItemSpacing.Y;
        var pageText = Loc.F("Seite {0} / {1}", state.PageIndex + 1, state.PageCount);
        var arrow = ImGui.GetFrameHeight();
        var width = arrow * 4 + ImGui.CalcTextSize(pageText).X + style.ItemSpacing.X * 4;
        var below = MathF.Max(style.ItemSpacing.Y, (room - gridHeight - arrow) / 2);
        ImGui.SetCursorPos(gridTopLeft + new Vector2(MathF.Max(0, (gridWidth - width) / 2), gridHeight + below));
        var first = state.PageIndex == 0;
        var last = state.PageIndex >= state.PageCount - 1;
        using (ImRaii.Disabled(first))
        {
            if (ImGui.Button("«##first", new Vector2(arrow)))
                state.ShowFirstPage();
        }
        if (!first && ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Erste Seite"));
        ImGui.SameLine();
        if (ImGui.ArrowButton("##prev", ImGuiDir.Left))
            state.PreviousPage();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        // A click on the page number lets one be typed.
        if (ImGui.Selectable($"{pageText}##page", false, ImGuiSelectableFlags.None, ImGui.CalcTextSize(pageText)))
        {
            typedPage = state.PageIndex + 1;
            ImGui.OpenPopup(PagePopup);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Klick: Seite eingeben\nMausrad: blättern, mit Umschalt 10 Seiten\nBild auf/ab, Pos1, Ende: blättern"));
        DrawPagePopup();
        ImGui.SameLine();
        if (ImGui.ArrowButton("##next", ImGuiDir.Right))
            state.NextPage();
        ImGui.SameLine();
        using (ImRaii.Disabled(last))
        {
            if (ImGui.Button("»##last", new Vector2(arrow)))
                state.ShowLastPage();
        }
        if (!last && ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Letzte Seite"));

        if (config.ShowMemoryStats)
        {
            ImGui.SetCursorPosY(MathF.Max(ImGui.GetCursorPosY(), gridTopLeft.Y + room));
            DrawMemoryStats(gridTopLeft.X, gridWidth);
        }
    }

    /// <summary>The debug memory line, centered below the page controls.</summary>
    private void DrawMemoryStats(float left, float gridWidth)
    {
        memory.Update();
        ImGui.SetCursorPosX(left + MathF.Max(0, (gridWidth - ImGui.CalcTextSize(memory.Line).X) / 2));
        ImGui.TextDisabled(memory.Line);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(MemoryStats.Tooltip);
    }

    private static string WeaponsLabel => Loc.T("Waffen gezogen");
    private static string DimLabel => Loc.T("Abdunkeln");

    private static float CheckboxWidth(string label) => ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label).X;

    /// <summary>
    /// Tile dye, jobs and "Modell-Duplikate ausblenden" right of the mode icons, as high as they are; the jobs (and the
    /// checkbox) wrap when the row is full.
    /// </summary>
    private void DrawFilterControls(float edge)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(0, spacing * 3);
        filterControls.DrawDye(edge);
        ImGui.SameLine(0, spacing * 3);
        if (ImGui.GetContentRegionAvail().X < filterControls.JobsWidth())
            ImGui.NewLine();
        filterControls.DrawJobs(edge);
        ImGui.SameLine(0, spacing * 3);
        if (ImGui.GetContentRegionAvail().X < TileFilterControls.DuplicatesWidth())
            ImGui.NewLine();
        filterControls.DrawDuplicates(edge);
        DrawUndoButtons(edge);
    }

    /// <summary>Undo and redo of the try-on, right-aligned in the row of the tile dye and the jobs (not while a set is edited).</summary>
    private void DrawUndoButtons(float edge)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = edge * 2 + spacing;
        ImGui.SameLine(0, spacing * 3);
        if (ImGui.GetContentRegionAvail().X < width)
            ImGui.NewLine();
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - width);
        using (ImRaii.Disabled(editor.IsOpen || !tryOn.TryOn.CanUndo))
        {
            if (IconButton.Draw("##undo", 0, FontAwesomeIcon.Undo, "", theme, edge))
                tryOn.Undo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Rückgängig"));
        ImGui.SameLine();
        using (ImRaii.Disabled(editor.IsOpen || !tryOn.TryOn.CanRedo))
        {
            if (IconButton.Draw("##redo", 0, FontAwesomeIcon.Redo, "", theme, edge))
                tryOn.Redo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Wiederholen"));
    }

    /// <summary>The display options' exact width, so the filter-column toggle ends flush with the right edge.</summary>
    private static float DisplayOptionsWidth()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return CheckboxWidth(WeaponsLabel) + 2 + WarningGlyphWidth() + spacing + CheckboxWidth(DimLabel) + spacing + ImGui.GetFrameHeight();
    }

    private static float WarningGlyphWidth()
    {
        using (UiScale.PushIcons())
            return ImGui.CalcTextSize(FontAwesomeIcon.ExclamationTriangle.ToIconString()).X;
    }

    /// <summary>Right of the slot bar: drawn weapons (slower, with a warning), dimming of missing items and the filter-column toggle.</summary>
    private void DrawDisplayOptions()
    {
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - DisplayOptionsWidth());
        var frameOffset = (ImGui.GetItemRectSize().Y - ImGui.GetFrameHeight()) / 2;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0, frameOffset));

        // All three items share the checkbox row's top; the (taller) warning glyph would otherwise push the second checkbox up.
        var rowY = ImGui.GetCursorPosY();
        var drawn = config.TileWeaponsDrawn;
        if (ImGui.Checkbox(WeaponsLabel, ref drawn))
            state.SetTileWeaponsDrawn(drawn);
        // The warning glyph is taller than the text: drawn by hand, centred on the checkbox.
        ImGui.SameLine(0, 2);
        var iconPos = new Vector2(ImGui.GetCursorScreenPos().X, ImGui.GetWindowPos().Y - ImGui.GetScrollY() + rowY);
        string glyph;
        Vector2 glyphSize;
        using (UiScale.PushIcons())
        {
            glyph = FontAwesomeIcon.ExclamationTriangle.ToIconString();
            glyphSize = ImGui.CalcTextSize(glyph);
        }
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.Dummy(new Vector2(glyphSize.X, ImGui.GetFrameHeight()));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Waffen-Kacheln mit gezogener Waffe: jede Kachel spielt das Ziehen ab, das Rendern dauert deutlich länger."));
        using (UiScale.PushIcons())
            ImGui.GetWindowDrawList().AddText(iconPos + new Vector2(0, (ImGui.GetFrameHeight() - glyphSize.Y) / 2), ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.3f, 1f)), glyph);

        ImGui.SameLine();
        ImGui.SetCursorPosY(rowY);
        var dim = config.DimMissingTiles;
        if (ImGui.Checkbox(DimLabel, ref dim))
        {
            config.DimMissingTiles = dim;
            config.Save();
        }

        // The filter column belongs to the display options; shown or hidden from here.
        ImGui.SameLine();
        ImGui.SetCursorPosY(rowY);
        if (IconButton.Draw("##filtercolumn", 0, FontAwesomeIcon.Filter, "", theme))
        {
            config.FilterColumnVisible = !config.FilterColumnVisible;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(config.FilterColumnVisible ? Loc.T("Filterspalte ausblenden") : Loc.T("Filterspalte einblenden"));
    }

    private void DrawTile(TileSubject subject, Vector2 size)
    {
        if (subject is FolderSubject folder)
        {
            folders.DrawTile(folder, size, dimmed: false);
            return;
        }
        const float rounding = 8f;
        var colors = theme.Colors;
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##tile{subject.Id.ItemId}-{subject.Id.Set.Kind}{subject.Id.Set.Value}", size);
        var hovered = ImGui.IsItemHovered();
        if (subject is SetSubject draggable && state.Filter.Mode == BrowseMode.MySets)
            folders.DragSource(draggable, size);
        // The right-click menu has the hand of a ring and the folder of an own set; other tiles have none.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && HasContextMenu(subject))
        {
            contextSubject = subject;
            ImGui.OpenPopup(ContextPopup);
        }
        // The star in the top-left corner puts the item or set on the wishlist; a click anywhere else selects the tile.
        var starPos = pos + new Vector2(6, 5);
        var starSize = WishStarSize();
        var canWish = wishlist.Current != null;
        var (starMin, starMax) = (starPos - new Vector2(3), starPos + starSize + new Vector2(3));
        var overStar = hovered && canWish && ImGui.IsMouseHoveringRect(starMin, starMax);
        // Decided where the button went down: a press on the star toggles the wish wherever it is released on the tile.
        var pressed = ImGui.GetIO().MouseClickedPos[0];
        var pressedOnStar = canWish && pressed.X >= starMin.X && pressed.Y >= starMin.Y && pressed.X <= starMax.X && pressed.Y <= starMax.Y;
        // An item has the heart of the favourites right of the star, decided the same way.
        var canFavor = state.Favorites.Current != null && subject is ItemSubject;
        var heartRect = GlyphRect(starPos + new Vector2(starSize.X + 8, 0), starSize);
        var overHeart = hovered && canFavor && ImGui.IsMouseHoveringRect(heartRect.Min, heartRect.Max);
        // An own set has "Bearbeiten" and "Löschen" below the star (while no set is edited), decided the same way.
        var ownSet = !editor.IsOpen && subject is SetSubject { Set.Id: { Kind: SetKind.Custom } ownId } ? customSets.Current?.Find(ownId.Value) : null;
        var editRect = GlyphRect(starPos + new Vector2(0, starSize.Y + 8), starSize);
        var deleteRect = GlyphRect(starPos + new Vector2(0, (starSize.Y + 8) * 2), starSize);
        var overEdit = hovered && ownSet != null && ImGui.IsMouseHoveringRect(editRect.Min, editRect.Max);
        var overDelete = hovered && ownSet != null && ImGui.IsMouseHoveringRect(deleteRect.Min, deleteRect.Max);
        // An item that others look like has a button bottom left, above the name strip: all of them as tiles (not while
        // they are shown already).
        var twins = subject is ItemSubject { Item: var twinOf } && state.OpenModel == null ? state.SameModelAs(twinOf.Id) : [];
        var twinsRect = GlyphRect(pos + new Vector2(6, size.Y - StripHeight(size.Y) - starSize.Y - 8), starSize);
        var overTwins = hovered && twins.Count > 0 && ImGui.IsMouseHoveringRect(twinsRect.Min, twinsRect.Max);
        // A dyeable item has a box per dye channel bottom right, at the height of that button: its dye, a click dyes only
        // this tile.
        var dyeItem = subject is ItemSubject { Item: { DyeCount: > 0 } dyeable } ? dyeable : null;
        var boxCount = dyeItem == null ? 0 : Math.Min(dyeItem.DyeCount, 2);
        var boxes = Enumerable.Range(0, boxCount)
            .Select(i => GlyphRect(new Vector2(pos.X + size.X - 6 - (boxCount - i) * starSize.X - (boxCount - 1 - i) * 8, twinsRect.Min.Y + 3), starSize))
            .ToArray();
        var overBox = hovered ? Array.FindIndex(boxes, b => ImGui.IsMouseHoveringRect(b.Min, b.Max)) : -1;
        var pressedBox = Array.FindIndex(boxes, b => Inside(pressed, b));
        if (clicked && pressedOnStar)
            wishlist.Toggle(subject);
        else if (clicked && canFavor && Inside(pressed, heartRect))
            state.Favorites.Toggle(subject);
        else if (clicked && twins.Count > 0 && Inside(pressed, twinsRect) && subject is ItemSubject { Item: var opened })
            state.ShowSameModel(opened);
        else if (clicked && pressedBox >= 0 && dyeItem != null)
        {
            dyeing = (dyeItem, pressedBox);
            ImGui.OpenPopup(TileDyePopup);
        }
        else if (clicked && ownSet != null && Inside(pressed, editRect))
            editor.StartEdit(ownSet);
        else if (clicked && ownSet != null && Inside(pressed, deleteRect))
        {
            deleteSet = ownSet;
            ImGui.OpenPopup(DeletePopup);
        }
        // Ctrl or Shift on an own set marks it for moving several at once; a plain click drops the marks.
        else if (clicked && subject is SetSubject markable && state.Filter.Mode == BrowseMode.MySets && folders.Click(markable))
        {
        }
        // While a custom set is edited, a tile goes into it instead of being selected.
        else if (clicked && editor.IsOpen)
        {
            editor.Put(subject, state.DyeFor(subject), RingHand(subject));
            if (subject is ItemSubject { Item: var used })
                state.History.Touch(used);
        }
        // Otherwise it is selected and put on in the try-on, dyed like its tile.
        else if (clicked)
        {
            if (subject is ItemSubject { Item: var used })
                state.History.Touch(used);
            state.Selected = subject;
            live.Select(subject);
            tryOn.Put(subject, state.DyeFor(subject), RingHand(subject));
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, hovered ? colors.CardRaised : colors.Card, rounding);

        var key = state.KeyFor(subject);
        // A set counts as owned when it is complete.
        var owned = subject is SetSubject set ? SetProgress.Of(set, IsOwned).Complete : IsOwned(subject.Wear[0].Id);
        // The other items of this item's model that are owned: the model button turns green, and a missing item's corner
        // half green.
        var ownedTwins = subject is ItemSubject { Item: var modelOf }
            ? state.SameModelAs(modelOf.Id).Where(t => t.Id != modelOf.Id && IsOwned(t.Id)).ToList()
            : [];
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
            var stripHeight = StripHeight(size.Y);
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
        // An item not on the wishlist whose model is, in another item: the star half amber, always shown.
        var wishedTwins = !listed && subject is ItemSubject { Item: var wishOf }
            ? state.SameModelAs(wishOf.Id).Where(t => t.Id != wishOf.Id && state.ItemSubjectFor(t.Id) is { } twin && wishlist.IsListed(twin)).ToList()
            : [];
        var showStar = listed || wishedTwins.Count > 0 || hovered && canWish;
        if (showStar)
        {
            var plain = overStar ? colors.Text : (colors.TextMuted & 0x00FFFFFF) | 0xA0000000;
            if (wishedTwins.Count > 0)
                DrawHalfWishStar(drawList, starPos, colors.Warning, plain);
            else
                DrawWishStar(drawList, starPos, listed ? colors.Warning : plain);
            if (overStar)
                ImGui.SetTooltip((listed ? Loc.T("Von der Wunschliste nehmen") : Loc.T("Auf die Wunschliste"))
                                 + (wishedTwins.Count > 0
                                     ? "\n" + Loc.F("Gleiches Modell auf der Wunschliste:\n{0}", string.Join("\n", wishedTwins.Take(5).Select(t => t.Name)))
                                     : ""));
        }
        // Like the star: pink when a favourite, faint on a hovered tile.
        var favored = subject is ItemSubject && state.Favorites.IsListed(subject);
        if (favored || overHeart || hovered && canFavor)
        {
            DrawTileGlyph(drawList, FontAwesomeIcon.Heart, heartRect,
                          favored ? FavoriteColor : overHeart ? colors.Text : (colors.TextMuted & 0x00FFFFFF) | 0xA0000000);
            if (overHeart)
                ImGui.SetTooltip(favored ? Loc.T("Aus den Favoriten nehmen") : Loc.T("Zu den Favoriten"));
        }
        // Always shown, unlike the star: it tells which items have others of their model; green when one of them is owned.
        if (twins.Count > 0)
        {
            var baseColor = ownedTwins.Count > 0 ? TileStyle.OwnedColor : overTwins ? colors.Text : colors.TextMuted;
            var color = overTwins ? baseColor | 0xFF000000 : (baseColor & 0x00FFFFFF) | 0xA0000000;
            DrawTileGlyph(drawList, FontAwesomeIcon.Cubes, twinsRect, color);
            drawList.AddText(new Vector2(twinsRect.Max.X + 2, twinsRect.Min.Y + 1), color, (twins.Count - 1).ToString());
            if (overTwins)
                ImGui.SetTooltip(Loc.F("Gleiches Modell: {0} weitere Items\nKlick: alle als Kacheln zeigen", twins.Count - 1)
                                 + (ownedTwins.Count > 0 ? "\n" + Loc.F("{0} davon im Besitz", ownedTwins.Count) : ""));
        }
        if (dyeItem != null)
            DrawDyeBoxes(drawList, dyeItem, state.DyeFor(subject), boxes, overBox);
        if (hovered && ownSet != null)
        {
            DrawTileGlyph(drawList, FontAwesomeIcon.Pen, editRect, overEdit ? colors.Text : (colors.TextMuted & 0x00FFFFFF) | 0xA0000000);
            DrawTileGlyph(drawList, FontAwesomeIcon.Trash, deleteRect, overDelete ? colors.Text : (colors.TextMuted & 0x00FFFFFF) | 0xA0000000);
            if (overEdit)
                ImGui.SetTooltip(Loc.T("Bearbeiten"));
            else if (overDelete)
                ImGui.SetTooltip(Loc.T("Löschen"));
        }
        // The places of star and heart stay reserved while they can show, so the marker does not jump when the tile is hovered.
        var markerX = canFavor || favored ? heartRect.Max.X - pos.X : showStar || canWish ? 6 + starSize.X + 3 : 6f;
        if (pool.Failures.TryGetValue(key, out var error))
        {
            drawList.AddText(pos + new Vector2(markerX, 4), ErrorColor, "!");
            var labelPos = pos + new Vector2(6, 4 + lineHeight);
            var label = Truncate(Loc.T("Renderfehler"), size.X - 12);
            drawList.AddText(labelPos, ErrorColor, label);
            // Like the trait icons, only the marker itself shows a tooltip: the reason of the failure.
            if (hovered && !overStar && !overHeart && ImGui.IsMouseHoveringRect(pos, labelPos + ImGui.CalcTextSize(label)))
                ImGui.SetTooltip(Loc.F("Renderfehler: {0}", error));
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
        // A missing item whose model is owned in another item (another variant, another job's version): the corner split
        // along its diagonal, the half at the edge green.
        if (!owned && ownedTwins.Count > 0)
        {
            drawList.AddTriangleFilled(corner, corner + new Vector2(-badge / 2, badge / 2), corner + new Vector2(0, badge), TileStyle.OwnedColor);
            if (hovered && ImGui.IsMouseHoveringRect(corner - new Vector2(badge, 0), corner + new Vector2(0, badge)))
                ImGui.SetTooltip(Loc.F("Nicht im Besitz, aber das gleiche Modell:\n{0}", string.Join("\n", ownedTwins.Take(5).Select(t => t.Name))));
        }
        if (folders.IsMarked(subject) && state.Filter.Mode == BrowseMode.MySets)
            folders.DrawMark(drawList, pos, size);
    }

    private bool IsOwned(uint itemId) => ownership.Current?.IsOwned(itemId) ?? false;

    /// <summary>A ring's tile (the hand) and an own set's tile in "Meine Sets" (the folder) have a right-click menu.</summary>
    private bool HasContextMenu(TileSubject subject) => subject switch
    {
        ItemSubject { Item.Slot: EquipSlot.RingRight or EquipSlot.RingLeft } => true,
        SetSubject { Set.Id.Kind: SetKind.Custom } => state.Filter.Mode == BrowseMode.MySets && customSets.Current is { Folders.Count: > 0 },
        _ => false,
    };

    /// <summary>
    /// The right-click menu of a tile: a ring on the right or the left hand, an own set into a folder. A set's pieces are
    /// behind the chest in the item card.
    /// </summary>
    /// <summary>
    /// The dye boxes of an item tile: the colour it shows on each channel, an accent frame where the tile has its own dye
    /// instead of the tile dye.
    /// </summary>
    private void DrawDyeBoxes(ImDrawListPtr drawList, GlamourItem item, DyePair dye, (Vector2 Min, Vector2 Max)[] boxes, int over)
    {
        var colors = theme.Colors;
        var own = state.HasOwnDye(item);
        for (var i = 0; i < boxes.Length; i++)
        {
            var (min, max) = boxes[i];
            var stain = i == 0 ? dye.First : dye.Second;
            drawList.AddRectFilled(min, max, 0xA0000000, 3f);
            if (stain != 0 && dyes.Find(stain) is { } color)
                drawList.AddRectFilled(min + Vector2.One, max - Vector2.One, color.Color, 3f);
            drawList.AddRect(min, max, i == over ? colors.Text : own ? colors.Accent : colors.Border, 3f, ImDrawFlags.None, own || i == over ? 2f : 1f);
            if (i == over)
                ImGui.SetTooltip(Loc.F(i == 0 ? "Farbe 1: {0}" : "Farbe 2: {0}", dyes.NameOf(stain)) + "\n" + Loc.T("Klick: nur diese Kachel umfärben"));
        }
    }

    /// <summary>The dyes for the channel of the tile whose box was clicked; a pick dyes that tile only.</summary>
    private void DrawTileDyePopup()
    {
        if (dyeing is not { } target || !ImGui.BeginPopup(TileDyePopup))
            return;
        var dye = state.DyeFor(new ItemSubject(target.Item));
        var stain = target.Channel == 0 ? dye.First : dye.Second;
        if (DyePicker.Palette(ref stain, dyes, theme))
            state.SetDye(target.Item, target.Channel == 0 ? dye with { First = stain } : dye with { Second = stain });
        ImGui.EndPopup();
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopup(ContextPopup))
            return;
        switch (contextSubject)
        {
            case SetSubject set:
                ImGui.TextDisabled(set.Name);
                folders.DrawMoveMenu(set);
                break;
            case ItemSubject ring:
                DrawRingHands(ring);
                break;
        }
        ImGui.EndPopup();
    }

    /// <summary>A ring's two entries, right and left hand (checked while it is worn there); a click on the tile picks the hand by itself.</summary>
    private void DrawRingHands(ItemSubject ring)
    {
        foreach (var hand in (EquipSlot[])[EquipSlot.RingRight, EquipSlot.RingLeft])
        {
            var worn = editor.IsOpen ? editor.Draft?.PieceIn(hand)?.ItemId == ring.Item.Id : tryOn.TryOn.PieceIn(hand)?.ItemId == ring.Item.Id;
            if (ImGui.MenuItem(Loc.F("{0} anziehen: {1}", SlotNames.Full(hand), ring.Item.Name) + $"##{hand}", "", worn))
                PutOn(ring, hand);
        }
    }

    /// <summary>The hand a ring's tile puts it on: the ring slot chosen in the slot bar (<see cref="SlotSelection.ClickedRingHand"/>).</summary>
    private EquipSlot? RingHand(TileSubject subject)
        => subject is ItemSubject { Item: var item } ? SlotSelection.ClickedRingHand(item, state.Filter.Slots) : null;

    /// <summary>Like a click on the subject's tile, but without selecting it; a ring on the hand in <paramref name="ring"/> when given.</summary>
    private void PutOn(TileSubject subject, EquipSlot? ring = null)
    {
        if (editor.IsOpen)
            editor.Put(subject, state.DyeFor(subject), ring);
        else
            tryOn.Put(subject, state.DyeFor(subject), ring);
    }

    /// <summary>The height of a tile's dark name strip (name, line, trait icons as far as the tile's height allows); 0 without one.</summary>
    public static float StripHeight(float tileHeight)
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var rows = TileInfo.InfoRows(tileHeight, lineHeight);
        return rows switch
        {
            0 => 0,
            3 => lineHeight * 2.2f + lineHeight * TraitIconScale + 10,
            _ => lineHeight * (rows + 0.2f) + 4 + rows,
        };
    }

    internal static (Vector2 Min, Vector2 Max) GlyphRect(Vector2 at, Vector2 size) => (at - new Vector2(3), at + size + new Vector2(3));

    internal static bool Inside(Vector2 point, (Vector2 Min, Vector2 Max) rect)
        => point.X >= rect.Min.X && point.Y >= rect.Min.Y && point.X <= rect.Max.X && point.Y <= rect.Max.Y;

    /// <summary>A glyph button on a tile (edit, delete), centred in its rect, with a dark shadow like the star.</summary>
    internal static void DrawTileGlyph(ImDrawListPtr drawList, FontAwesomeIcon icon, (Vector2 Min, Vector2 Max) rect, uint color)
    {
        using (UiScale.PushIcons())
        {
            var glyph = icon.ToIconString();
            var at = (rect.Min + rect.Max - ImGui.CalcTextSize(glyph)) / 2;
            drawList.AddText(at + Vector2.One, 0xC0000000, glyph);
            drawList.AddText(at, color, glyph);
        }
    }

    /// <summary>"Set „…“ löschen?" after the trash glyph of an own set's tile.</summary>
    private void DrawDeleteConfirm()
    {
        if (!ImGui.BeginPopup(DeletePopup))
            return;
        if (deleteSet is { } set)
        {
            ImGui.TextUnformatted(Loc.F("Set „{0}“ löschen?", set.Name));
            if (ImGui.Button(Loc.T("Löschen")))
            {
                customSets.Current?.Remove(set.Id);
                if (state.Selected is SetSubject { Set.Id: { Kind: SetKind.Custom } selected } && selected.Value == set.Id)
                {
                    state.Selected = null;
                    live.Select(null);
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.T("Abbrechen")))
                ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

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

    /// <summary>The star's left half in <paramref name="half"/>, the right half in <paramref name="rest"/>, like the split ownership corner.</summary>
    private static void DrawHalfWishStar(ImDrawListPtr drawList, Vector2 at, uint half, uint rest)
    {
        DrawWishStar(drawList, at, rest);
        var size = WishStarSize();
        drawList.PushClipRect(at, at + new Vector2(size.X / 2, size.Y + 2), true);
        DrawWishStar(drawList, at, half);
        drawList.PopClipRect();
    }

    internal static Vector2 WishStarSize()
    {
        using (UiScale.PushIcons())
            return ImGui.CalcTextSize(FontAwesomeIcon.Star.ToIconString());
    }

    /// <summary>The tile's texts and icons from the per-subject cache, built on the first frame the tile shows.</summary>
    private TileLabels LabelsFor(TileSubject subject, bool owned, float width)
    {
        var key = (subject.Id, (subject as SetSubject)?.Variant ?? 0, subject.Name);
        if (labels.TryGetValue(key, out var cached))
            return cached;
        return labels[key] = subject switch
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
        if (!windowHovered || !ImGui.IsMouseHoveringRect(at, at + new Vector2(iconSize)))
            return;
        if (icon.Kind == TraitKind.Trade && icon.IconId == TraitIcons.Marketable && subject is ItemSubject { Item: var item })
            MarketTooltip.Draw(market, item.Id);
        else
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
