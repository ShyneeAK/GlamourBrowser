using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using Dalamud.Utility;
using GlamourBrowser.Sources;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;
using GlamourBrowser.GlamourPlate;
using GlamourBrowser.Market;

namespace GlamourBrowser.UI;

/// <summary>Left column: large live preview, slot bar and the selected item's card with its actions.</summary>
public sealed unsafe class LivePane
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly LivePreview live;
    private readonly CalibrationService calibration;
    private readonly JobCatalog jobs;
    private readonly Theme.Theme theme;
    private readonly SourceService sources;
    private readonly CharacterListService wishlist;
    private readonly CustomSetService customSets;
    private readonly DyeCatalog dyes;
    private readonly CustomSetEditor editor;
    private readonly SetEditorPane editorPane;
    private readonly GlamourPlateBridge plates;
    private readonly TryOnService tryOn;
    private readonly MarketService market;
    private readonly Configuration config;
    private readonly NativeItemTooltip itemTooltip;
    // When the try-on was last saved as a set, for a short confirmation next to the button.
    private DateTime? savedAt;
    private string savedName = "";
    private const string SaveSetPopup = "##saveset";
    // The name typed in the popup of "Als Set speichern".
    private string setName = "";
    private readonly HashSet<(uint Item, SourceCategory Category)> expanded = new();
    // Pieces of the selected set whose sources are open.
    private readonly HashSet<uint> openPieces = new();
    private const int SourcesPerCategory = 3;
    private const float CardFrameScale = 1.4f;
    private const string OutsideFiltersPopup = "##outside-filters";
    // The worn piece the filters hide, named by the hint popup.
    private string outsideFilters = "";

    public LivePane(BrowserState state, OwnershipService ownership, LivePreview live, CalibrationService calibration, JobCatalog jobs, Theme.Theme theme,
                    SourceService sources, CharacterListService wishlist, CustomSetService customSets, DyeCatalog dyes, CustomSetEditor editor,
                    SetEditorPane editorPane, GlamourPlateBridge plates, TryOnService tryOn, MarketService market, Configuration config,
                    NativeItemTooltip itemTooltip)
    {
        this.itemTooltip = itemTooltip;
        this.market = market;
        this.config = config;
        this.tryOn = tryOn;
        this.plates = plates;
        this.editor = editor;
        this.editorPane = editorPane;
        this.sources = sources;
        this.wishlist = wishlist;
        this.customSets = customSets;
        this.dyes = dyes;
        this.state = state;
        this.ownership = ownership;
        this.live = live;
        this.calibration = calibration;
        this.jobs = jobs;
        this.theme = theme;
    }

    public void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        DrawLive(new Vector2(avail.X, MathF.Min(avail.X * 960f / 576f, avail.Y * 0.55f)));
        // The set editor has its own buttons; the try-on waits behind it.
        if (!editor.IsOpen)
            DrawTryOnButtons();

        ImGui.Spacing();
        using var card = ImRaii.Child("##itemcard", Vector2.Zero, true);
        if (!card.Success)
            return;
        PlateTransferUi.DrawResult(plates);
        // While a custom set is edited, the editor takes the card's place.
        if (editor.IsOpen)
            editorPane.Draw();
        else
        {
            // Larger buttons and icons than elsewhere: frames 1.4 times as high, text centred in them.
            var style = ImGui.GetStyle();
            var padding = (ImGui.GetFrameHeight() * CardFrameScale - ImGui.GetFontSize()) / 2;
            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, style.FramePadding with { Y = padding }))
                DrawItemCard();
        }
    }

    /// <summary>The size of the item's dye fields in the card's top-right corner: two thirds of the item icon.</summary>
    private static float DyeEdge => 40 * UiScale.Global * 2 / 3;

    /// <summary>The width the item's dye fields take in the card's top-right corner; 0 while it is not worn.</summary>
    private float DyeBoxesWidth(GlamourItem item)
        => tryOn.TryOn.SlotOf(item.Id) is { } slot && tryOn.TryOn.PieceIn(slot) != null
            ? ImGui.CalcTextSize(Loc.T("Farbe")).X + DyeEdge * 2 + ImGui.GetStyle().ItemSpacing.X * 2
            : 0;

    /// <summary>
    /// The item's two dye fields in the try-on behind "Farbe", large in the card's top-right corner (the cursor stays where it was),
    /// crossed out where it has no channel; none while it is not worn.
    /// </summary>
    private void DrawDyeBoxes(GlamourItem item, Vector2 topRight)
    {
        if (tryOn.TryOn.SlotOf(item.Id) is not { } slot || tryOn.TryOn.PieceIn(slot) is not { } piece)
            return;
        var (first, second) = DyeBoxes.For(item.DyeCount);
        var cursor = ImGui.GetCursorPos();
        var start = topRight - new Vector2(DyeBoxesWidth(item), 0);
        var label = Loc.T("Farbe");
        var labelSize = ImGui.CalcTextSize(label);
        ImGui.GetWindowDrawList().AddText(start + new Vector2(0, (DyeEdge - labelSize.Y) / 2), ImGui.GetColorU32(ImGuiCol.TextDisabled), label);
        ImGui.SetCursorScreenPos(start + new Vector2(labelSize.X + ImGui.GetStyle().ItemSpacing.X, 0));
        var stain0 = piece.Stain0;
        if (DyePicker.Draw("##itemdye1", ref stain0, dyes, theme, first, Loc.T("Farbe 1"), DyeEdge))
            tryOn.TryOn.SetStain(slot, 1, stain0, item.DyeCount);
        var stain1 = piece.Stain1;
        ImGui.SameLine();
        if (DyePicker.Draw("##itemdye2", ref stain1, dyes, theme, second, Loc.T("Farbe 2"), DyeEdge))
            tryOn.TryOn.SetStain(slot, 2, stain1, item.DyeCount);
        ImGui.SetCursorPos(cursor);
    }

    /// <summary>
    /// The chest under the dye fields (top right without them) for an item of a gear set: shows the set's pieces as tiles
    /// in the grid, until its back button; with several sets a menu picks one. The text below starts under it.
    /// </summary>
    private void DrawSetChest(IReadOnlyList<SetSubject> sets, Vector2 topRight)
    {
        if (sets.Count == 0)
            return;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(topRight - new Vector2(DyeEdge, 0));
        var tooltip = sets.Count == 1
            ? Loc.F("„{0}“: alle Teile als Kacheln zeigen", sets[0].Name)
            : Loc.F("Gehört zu {0} Ausrüstungs-Sets: eins auswählen und seine Teile als Kacheln zeigen", sets.Count);
        if (IconButton.Draw("##setchest", SlotIcons.Sets, FontAwesomeIcon.LayerGroup, tooltip, theme, DyeEdge))
        {
            if (sets.Count == 1)
                state.ShowSetPieces(sets);
            else
                ImGui.OpenPopup("##setchest-sets");
        }
        if (ImGui.BeginPopup("##setchest-sets"))
        {
            for (var i = 0; i < sets.Count; i++)
            {
                if (ImGui.Selectable($"{sets[i].Name}##set{i}"))
                    state.ShowSetPieces([sets[i]]);
            }
            ImGui.EndPopup();
        }
        var bottom = topRight.Y + DyeEdge + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.SetCursorScreenPos(cursor with { Y = MathF.Max(cursor.Y, bottom) });
    }

    /// <summary>Below the live view: back to the try-on's basis (tile mode), or the whole try-on as a new custom set.</summary>
    private void DrawTryOnButtons()
    {
        using (ImRaii.Disabled(!tryOn.TryOn.HasChanges))
        {
            if (ImGui.Button(Loc.T("Zurücksetzen")))
                tryOn.Reset();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Zurück auf aktuelles Outfit bzw. Unterwäsche, je nach Kachel-Modus"));

        ImGui.SameLine();
        var store = customSets.Current;
        // Like the set editor: a set needs a piece the catalog knows, else it would not show under "Meine Sets".
        var hasKnownPiece = tryOn.TryOn.Pieces.Any(p => state.ItemOf(p.ItemId) != null);
        using (ImRaii.Disabled(store == null || !hasKnownPiece))
        {
            if (ImGui.Button(Loc.T("Als Set speichern")))
            {
                setName = "";
                ImGui.OpenPopup(SaveSetPopup);
            }
        }
        DrawSaveSetPopup(store);
        if (savedAt is { } at && DateTime.UtcNow - at < TimeSpan.FromSeconds(3))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Loc.F("Als ‚{0}‘ gespeichert", savedName));
        }

        DrawWornPieces();
        DyeCounter.Draw(tryOn.TryOn.Pieces.Select(p => (p.Stain0, p.Stain1, state.ItemOf(p.ItemId)?.DyeCount ?? 0)), dyes, theme);
    }

    /// <summary>
    /// The name for the try-on as a new set: saved on Enter or "Speichern" into the open folder of "Meine Sets", and the
    /// view stays where it is (a quick save; editing is on the set's tile). Empty, it is called "Neues Set".
    /// </summary>
    private void DrawSaveSetPopup(CustomSetStore? store)
    {
        if (!ImGui.BeginPopup(SaveSetPopup))
            return;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(260 * UiScale.Global);
        // The limit is in UTF-8 bytes, as in the set editor: room for umlauts, the store cuts the name to its length.
        var entered = ImGui.InputTextWithHint("##setname", CustomSetStore.DefaultName, ref setName, CustomSetStore.MaxNameLength * 4 + 1,
                                              ImGuiInputTextFlags.EnterReturnsTrue);
        var save = ImGui.Button(Loc.T("Speichern")) || entered;
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Abbrechen")))
            ImGui.CloseCurrentPopup();
        // Logged out in the meantime, the popup just closes.
        if (save)
        {
            if (store != null)
            {
                var name = setName.Trim().Length > 0 ? setName : CustomSetStore.DefaultName;
                savedName = store.Add(name, tryOn.TryOn.Pieces, DateTimeOffset.Now, state.Filter.Mode == BrowseMode.MySets ? state.OpenFolder : 0).Name;
                savedAt = DateTime.UtcNow;
            }
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    /// <summary>
    /// One icon per piece the try-on wears (known items, in slot order): a click selects it again, for its card, dyes and
    /// camera, without putting anything on, and shows its slot's tiles on the page with it; a right-click takes it off. Wraps when the pane is narrow.
    /// </summary>
    private void DrawWornPieces()
    {
        var size = new Vector2(ImGui.GetFrameHeight() * 2);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var first = true;
        foreach (var piece in tryOn.TryOn.Pieces)
        {
            if (state.ItemSubjectFor(piece.ItemId, piece.Slot) is not { } subject)
                continue;
            if (!first)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + size.X > right)
                    ImGui.NewLine();
            }
            first = false;
            var selected = state.Selected is ItemSubject { Item.Id: var id } && id == piece.ItemId;
            if (IconToggle.Draw($"##worn{(int)piece.Slot}", subject.Item.IconId, size, selected,
                    $"{SlotNames.Full(piece.Slot)}: {subject.Name}\n" + Loc.T("Klick: auswählen und in den Kacheln zeigen\nRechtsklick: ausziehen"),
                    theme, FontAwesomeIcon.Tshirt))
            {
                state.Selected = subject;
                live.Select(subject);
                if (!state.ShowInGrid(subject.Item) && config.HintPiecesOutsideFilters)
                {
                    outsideFilters = subject.Name;
                    ImGui.OpenPopup(OutsideFiltersPopup);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                tryOn.TryOn.TakeOff(piece.Slot);
        }
        DrawOutsideFiltersHint();
    }

    /// <summary>
    /// The filters stay when a worn piece is clicked; when they hide it, this says why its tile is missing. In the middle
    /// of the screen, where the eyes are, not at the mouse.
    /// </summary>
    private void DrawOutsideFiltersHint()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + viewport.Size / 2, ImGuiCond.Appearing, new Vector2(0.5f));
        if (!ImGui.BeginPopup(OutsideFiltersPopup))
            return;
        ImGui.TextUnformatted(Loc.F("{0} ist mit den aktuellen Filtern nicht zu finden.", outsideFilters));
        var hide = !config.HintPiecesOutsideFilters;
        if (ImGui.Checkbox(Loc.T("Nicht mehr anzeigen"), ref hide))
        {
            config.HintPiecesOutsideFilters = !hide;
            config.Save();
        }
        ImGui.Spacing();
        if (ImGui.Button("OK"))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawLive(Vector2 area)
    {
        var colors = theme.Colors;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var texture = live.Texture;

        // Without a selection the view shows the own outfit, so only a view still being created is empty.
        if (texture == null || texture->D3D11ShaderResourceView == null)
        {
            ImGui.Dummy(area);
            drawList.AddRectFilled(origin, origin + area, colors.Card, 8f);
            var hint = Loc.T("Live-Ansicht wird vorbereitet …");
            drawList.AddText(origin + (area - ImGui.CalcTextSize(hint)) / 2, colors.TextMuted, hint);
            return;
        }

        // Keep the CharaView aspect (576:960) inside the available area, centred horizontally.
        var height = area.Y;
        var width = MathF.Min(area.X, height * 576f / 960f);
        var size = new Vector2(width, width * 960f / 576f);
        var pos = origin + new Vector2((area.X - width) / 2, 0);
        var uv1 = new Vector2((float)texture->ActualWidth / texture->AllocatedWidth, (float)texture->ActualHeight / texture->AllocatedHeight);

        drawList.AddRectFilled(origin, origin + area, colors.Card, 8f);
        ImGui.SetCursorScreenPos(pos);
        // The invisible button captures mouse drags so the window does not move; the buttons drawn over it take their clicks.
        ImGui.InvisibleButton("##liveview", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        ImGui.SetItemAllowOverlap();
        drawList.AddImageRounded(new ImTextureID(texture->D3D11ShaderResourceView), pos, pos + size, Vector2.Zero, uv1, 0xFFFFFFFF, 8f);

        var io = ImGui.GetIO();
        if (ImGui.IsItemActive())
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                live.QueueRotate(io.MouseDelta.X, io.MouseDelta.Y);
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
                live.QueuePan(io.MouseDelta.X, io.MouseDelta.Y);
        }
        if (ImGui.IsItemHovered())
        {
            if (io.MouseWheel != 0)
                live.QueueZoom(io.MouseWheel);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                live.QueueReset();
        }

        DrawHelpIcon(drawList, origin + new Vector2(area.X, 0));

        // In the view's top-left corner, with the game's own round buttons of its try-on window (a gold ring while on):
        // everything on or off (the tile mode: current outfit or underwear), the visor on the helmet whatever is selected,
        // the weapon toggles everywhere but on sets (which always show the weapon sheathed).
        var edge = ImGui.GetFrameHeight() * 1.3f;
        var buttonPos = pos + new Vector2(8, 8);
        var step = new Vector2(0, edge + ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorScreenPos(buttonPos);
        var outfit = config.TileMode == TileMode.CurrentOutfit;
        if (RoundButton.Draw("##outfit", TryonIcons.Outfit, edge, outfit,
                outfit ? Loc.T("Alles ablegen (Unterwäsche)") : Loc.T("Alles anlegen (aktuelles Outfit)"), theme, FontAwesomeIcon.User))
            state.SetTileMode(outfit ? TileMode.Underwear : TileMode.CurrentOutfit);
        ImGui.SetCursorScreenPos(buttonPos += step);
        var keep = tryOn.TryOn.Keep;
        if (RoundButton.Draw("##keep", TryonIcons.Keep, edge, keep,
                keep ? Loc.T("Anprobierte Teile behalten: an\nJeder Klick kommt zu den Teilen davor dazu")
                     : Loc.T("Anprobierte Teile behalten: aus\nNur das zuletzt angeklickte Teil wird anprobiert"), theme, FontAwesomeIcon.Thumbtack))
            tryOn.SetKeep(!keep);
        ImGui.SetCursorScreenPos(buttonPos += step);
        if (RoundButton.Draw("##visor", TryonIcons.Visor, edge, live.VisorClosed,
                live.VisorClosed ? Loc.T("Visier öffnen") : Loc.T("Visier schließen (nur Helme mit Visier)"), theme, FontAwesomeIcon.HardHat))
            live.VisorClosed = !live.VisorClosed;
        if (state.Selected is not SetSubject)
        {
            // Without a main hand (taken off) there is nothing to draw or show.
            var noWeapon = !live.HasMainHand;
            using (ImRaii.Disabled(noWeapon))
            {
                ImGui.SetCursorScreenPos(buttonPos += step);
                if (RoundButton.Draw("##draw", TryonIcons.DrawWeapon, edge, live.WeaponDrawn,
                        live.WeaponDrawn ? Loc.T("Waffe wegstecken") : Loc.T("Waffe ziehen"), theme, FontAwesomeIcon.FistRaised))
                    live.WeaponDrawn = !live.WeaponDrawn;
                NoWeaponHint(noWeapon);
                ImGui.SetCursorScreenPos(buttonPos + step);
                if (RoundButton.Draw("##weaponvisible", TryonIcons.ShowWeapon, edge, live.WeaponVisible,
                        live.WeaponVisible ? Loc.T("Waffe ausblenden") : Loc.T("Waffe anzeigen"), theme, FontAwesomeIcon.Eye))
                {
                    live.WeaponVisible = !live.WeaponVisible;
                    // The tiles follow: hidden there too, but on tiles of weapons.
                    state.SetWeaponsHidden(!live.WeaponVisible);
                }
                NoWeaponHint(noWeapon);
            }
        }

        // Bottom-right: how fast dragging and the wheel move the view, at hand where it is used.
        ImGui.SetCursorScreenPos(pos + size - new Vector2(edge + 8, edge + 8));
        if (RoundButton.Draw("##livesettings", TryonIcons.Settings, edge, false, Loc.T("Empfindlichkeit der Live-Ansicht"), theme, FontAwesomeIcon.Cog))
            ImGui.OpenPopup("##livesensitivity");
        DrawSensitivityPopup();
        ImGui.SetCursorScreenPos(origin + new Vector2(0, area.Y + ImGui.GetStyle().ItemSpacing.Y));
    }

    private static void NoWeaponHint(bool noWeapon)
    {
        if (noWeapon && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Keine Waffe angezogen"));
    }

    private void DrawSensitivityPopup()
    {
        if (!ImGui.BeginPopup("##livesensitivity"))
            return;
        ImGui.TextDisabled(Loc.T("Empfindlichkeit der Live-Ansicht"));
        var rotate = config.RotateSensitivity;
        var pan = config.PanSensitivity;
        var zoom = config.ZoomSensitivity;
        ImGui.SetNextItemWidth(180 * UiScale.Global);
        var changed = ImGui.SliderFloat(Loc.T("Drehen"), ref rotate, 0.01f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        ImGui.SetNextItemWidth(180 * UiScale.Global);
        changed |= ImGui.SliderFloat(Loc.T("Verschieben"), ref pan, 0.01f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        ImGui.SetNextItemWidth(180 * UiScale.Global);
        changed |= ImGui.SliderFloat(Loc.T("Zoom"), ref zoom, 0.1f, 20f, "%.2f", ImGuiSliderFlags.Logarithmic);
        if (changed)
        {
            config.RotateSensitivity = rotate;
            config.PanSensitivity = pan;
            config.ZoomSensitivity = zoom;
            config.Save();
        }
        ImGui.EndPopup();
    }

    /// <summary>
    /// Controls help as a small "?" in the top-right corner; the tooltip only shows over the icon,
    /// not while dragging the preview.
    /// </summary>
    private void DrawHelpIcon(ImDrawListPtr drawList, Vector2 topRight)
    {
        string glyph;
        Vector2 glyphSize;
        using (UiScale.PushIcons())
        {
            glyph = FontAwesomeIcon.QuestionCircle.ToIconString();
            glyphSize = ImGui.CalcTextSize(glyph);
        }

        var min = topRight + new Vector2(-glyphSize.X - 8, 8);
        var hovered = ImGui.IsMouseHoveringRect(min, min + glyphSize) && !ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Right);
        using (UiScale.PushIcons())
            drawList.AddText(min, hovered ? theme.Colors.Text : theme.Colors.TextMuted, glyph);
        if (hovered)
            ImGui.SetTooltip(Loc.T("Links ziehen: drehen\nRechts ziehen: verschieben\nMausrad: Zoom\nDoppelklick: zurücksetzen"));
    }

    private void DrawItemCard()
    {
        if (state.Selected is SetSubject set)
        {
            DrawSetCard(set);
            return;
        }
        if (state.Selected is not ItemSubject { Item: var item } subject)
        {
            ImGui.TextDisabled(Loc.T("Kein Item ausgewählt"));
            return;
        }

        var colors = theme.Colors;
        // The dye fields sit in the top-right corner; name and level wrap before them.
        var topRight = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X, 0);
        var sets = state.SetsOf(item.Id);
        var rightWidth = MathF.Max(DyeBoxesWidth(item), sets.Count > 0 ? DyeEdge : 0);
        var wrapAt = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - rightWidth - ImGui.GetStyle().ItemSpacing.X;
        GameIcon.Draw(item.IconId, new Vector2(40 * UiScale.Global), FontAwesomeIcon.Tshirt, colors.TextMuted);
        // The game's own tooltip, as in the armoire or the dresser.
        if (ImGui.IsItemHovered())
            itemTooltip.Hover(item.Id, ImGui.GetItemRectMin());
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(wrapAt);
        ImGui.TextWrapped(item.Name);
        var patch = sources.IsReady ? sources.Index.PatchOf(item.Id) : 0;
        // The patch keeps its dot in every language.
        using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
            ImGui.TextWrapped(Loc.F("Level {0}", item.LevelEquip)
                              + (patch > 0 ? " · " + Loc.F("Patch {0}", patch.ToString("0.0#", CultureInfo.InvariantCulture)) : ""));
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        DrawDyeBoxes(item, topRight);
        DrawSetChest(sets, topRight + new Vector2(0, DyeBoxesWidth(item) > 0 ? DyeEdge + ImGui.GetStyle().ItemSpacing.Y : 0));

        DrawTraits(item);
        ImGui.Spacing();
        DrawJobs(item);
        ImGui.Spacing();
        DrawLocations(ownership, item);

        ImGui.Separator();
        if (DrawWishStar(subject))
            ImGui.SameLine();
        if (IconButton.Draw("##tryon", 0, FontAwesomeIcon.Tshirt, Loc.T("Im Anprobe-Fenster des Spiels öffnen"), theme))
            Plugin.Framework.RunOnFrameworkThread(() => AgentTryon.TryOn(0, item.Id));
        ImGui.SameLine();
        if (IconButton.Draw("##chatlink", 0, FontAwesomeIcon.CommentDots, Loc.T("Chat-Link: schreibt einen Item-Link in deinen eigenen Chat und kopiert den Namen."), theme))
        {
            Plugin.ChatGui.Print(new SeStringBuilder().AddText("Shynees Glamour Dresser: ").AddItemLink(item.Id, false).Build());
            ImGui.SetClipboardText(item.Name);
        }
        ImGui.SameLine();
        if (IconButton.Draw("##copyname", 0, FontAwesomeIcon.Copy, Loc.T("Namen kopieren"), theme))
            ImGui.SetClipboardText(item.Name);
        ImGui.SameLine();
        DrawCalibration(item);

        ImGui.Separator();
        DrawSources(item);
        DrawLinks(item, atBottom: true);
    }

    /// <summary>The wishlist star of the card, like the tile's; false (nothing drawn) while the wishlist is not loaded.</summary>
    private bool DrawWishStar(TileSubject subject)
    {
        if (wishlist.Current == null)
            return false;
        var listed = wishlist.IsListed(subject);
        if (IconToggle.Draw("##wish", 0, new Vector2(ImGui.GetFrameHeight()), listed,
                            listed ? Loc.T("Von der Wunschliste nehmen") : Loc.T("Auf die Wunschliste"), theme, FontAwesomeIcon.Star))
            wishlist.Toggle(subject);
        return true;
    }

    /// <summary>Name, progress and origin of a set, then its pieces: owned or not, where they are or come from; a click opens a piece's sources.</summary>
    private void DrawSetCard(SetSubject set)
    {
        var colors = theme.Colors;
        bool IsOwned(uint id) => ownership.Current?.IsOwned(id) ?? false;
        var progress = SetProgress.Of(set, IsOwned);
        var body = set.Pieces.FirstOrDefault(p => p.Slot == EquipSlot.Body) ?? set.Pieces[0];
        // The chest in the top-right corner shows the pieces as tiles; the name wraps before it.
        var topRight = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X, 0);
        var wrapAt = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - DyeEdge - ImGui.GetStyle().ItemSpacing.X;
        GameIcon.Draw(body.IconId, new Vector2(40 * UiScale.Global), FontAwesomeIcon.LayerGroup, colors.TextMuted);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(wrapAt);
        ImGui.TextWrapped(set.Name);
        ImGui.PopTextWrapPos();
        var origin = set.Set.Id.Kind switch
        {
            SetKind.Dresser => Loc.T("Kommoden-Set"),
            SetKind.MogStation => Loc.T("Mogstation-Set"),
            SetKind.Custom => Loc.T("Eigenes Set"),
            _ => Loc.T("Ausrüstungs-Set"),
        };
        ImGui.TextDisabled($"{progress.Owned}/{progress.Total} · {origin}");
        ImGui.EndGroup();
        DrawSetChest([set], topRight);
        // Editing and deleting are on the tile; the card has the wishlist star and the transfer into the open plate.
        var starred = DrawWishStar(set);
        if (set.Set.Id.Kind == SetKind.Custom && customSets.Current?.Find(set.Set.Id.Value) is { } toTransfer)
        {
            if (starred)
                ImGui.SameLine();
            PlateTransferUi.DrawButton(plates, toTransfer.Pieces);
        }
        // An own set's dyes (detected sets have none).
        DyeCounter.Draw(set.Pieces.Select(p => (set.StainsOf(p.Slot).Stain0, set.StainsOf(p.Slot).Stain1, p.DyeCount)), dyes, theme);
        ImGui.Separator();

        var iconSize = new Vector2(ImGui.GetFrameHeight());
        foreach (var piece in set.Pieces)
        {
            var owned = IsOwned(piece.Id);
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, owned ? colors.Accent : colors.TextMuted))
            using (UiScale.PushIcons())
                ImGui.TextUnformatted((owned ? FontAwesomeIcon.Check : FontAwesomeIcon.Times).ToIconString());
            ImGui.SameLine();
            GameIcon.Draw(piece.IconId, iconSize, FontAwesomeIcon.Tshirt, colors.TextMuted);
            ImGui.SameLine();
            var open = openPieces.Contains(piece.Id);
            // As wide as the name only, so the place or source fits behind it.
            if (ImGui.Selectable($"{piece.Name}##piece{piece.Id}", open, ImGuiSelectableFlags.None, new Vector2(ImGui.CalcTextSize(piece.Name).X, 0)))
            {
                if (!openPieces.Remove(piece.Id))
                    openPieces.Add(piece.Id);
            }
            DrawDyeDots(set, piece.Slot);
            ImGui.SameLine();
            ImGui.TextDisabled(owned ? WhereOwned(piece) : BestSource(piece));
            if (!open)
                continue;
            using (ImRaii.PushId((int)piece.Id))
            using (ImRaii.PushIndent(iconSize.X * 2 + ImGui.GetStyle().ItemSpacing.X * 2))
            {
                DrawSources(piece);
                DrawLinks(piece, atBottom: false);
            }
            ImGui.Separator();
        }
    }

    /// <summary>A small dot per dye of the slot (custom sets), the dye's name as tooltip.</summary>
    private void DrawDyeDots(SetSubject set, EquipSlot slot)
    {
        var (stain0, stain1) = set.StainsOf(slot);
        DrawDyeDot(stain0);
        DrawDyeDot(stain1);
    }

    private void DrawDyeDot(byte stain)
    {
        if (stain == 0)
            return;
        ImGui.SameLine();
        // Centred on the row, which is a frame high (the piece icons).
        var row = ImGui.GetFrameHeight();
        var radius = ImGui.GetTextLineHeight() * 0.35f;
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(radius * 2, row));
        ImGui.GetWindowDrawList().AddCircleFilled(min + new Vector2(radius, row / 2), radius, dyes.Find(stain)?.Color ?? theme.Colors.TextMuted);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(dyes.NameOf(stain));
    }

    /// <summary>Deletes a custom set after asking.</summary>
    private string WhereOwned(GlamourItem piece) => ownership.Current?.LocationsOf(piece.Id).FirstOrDefault()?.DisplayName ?? "";

    /// <summary>The source of the piece's highest ranked category, as "Kategorie: Titel".</summary>
    private string BestSource(GlamourItem piece)
    {
        if (!sources.IsReady || SourceRanking.Top(sources.Index.KnownCategoriesOf(piece.Id)) is not { } top)
            return "";
        var source = sources.Index.SourcesOf(piece.Id).FirstOrDefault(s => s.Category == top);
        return source == null ? SourceCategories.DisplayName(top) : $"{SourceCategories.DisplayName(top)}: {source.Title}";
    }

    private void DrawSources(GlamourItem item)
    {
        ImGui.TextUnformatted(Loc.T("Quellen"));
        if (!sources.IsReady)
        {
            ImGui.TextDisabled(Loc.T("Quellen werden geladen …"));
            return;
        }

        var all = sources.Index.SourcesOf(item.Id);
        if (all.Count == 0)
            ImGui.TextDisabled(Loc.T("Keine Quelle bekannt"));

        var iconSize = new Vector2(ImGui.GetFrameHeight());
        foreach (var group in all.GroupBy(s => s.Category).OrderBy(g => g.Key))
        {
            var iconMin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(iconSize);
            TileGrid.DrawSourceIcon(ImGui.GetWindowDrawList(), group.Key, iconMin, iconSize.X, theme.Colors.TextMuted);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(SourceCategories.DisplayName(group.Key));

            var key = (item.Id, group.Key);
            var list = group.ToList();
            var shown = expanded.Contains(key) ? list : list.Take(SourcesPerCategory).ToList();
            using (ImRaii.PushIndent(iconSize.X + ImGui.GetStyle().ItemSpacing.X))
            {
                for (var i = 0; i < shown.Count; i++)
                    DrawSource(shown[i], $"{group.Key}-{i}");

                if (list.Count > SourcesPerCategory && !expanded.Contains(key) && ImGui.Button(Loc.F("weitere ({0}) …", list.Count - SourcesPerCategory) + $"##more{group.Key}"))
                    expanded.Add(key);
            }
        }
    }

    /// <summary>Garland Tools, Teamcraft and a web search for the item.</summary>
    /// <param name="atBottom">At the card's bottom-left while the card has room (the item card); below the sources otherwise.</param>
    private void DrawLinks(GlamourItem item, bool atBottom)
    {
        ImGui.Spacing();
        var bottom = ImGui.GetWindowHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetFrameHeight();
        if (atBottom && ImGui.GetScrollMaxY() == 0 && ImGui.GetCursorPosY() < bottom)
            ImGui.SetCursorPosY(bottom);
        if (IconButton.Draw("##garland", 0, FontAwesomeIcon.ExternalLinkAlt, "Garland Tools", theme))
            Util.OpenLink($"https://www.garlandtools.org/db/#item/{item.Id}");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Garland");
        ImGui.SameLine();
        if (IconButton.Draw("##teamcraft", 0, FontAwesomeIcon.ExternalLinkAlt, "Teamcraft", theme))
            // In the plugin's language; Teamcraft knows the same four.
            Util.OpenLink($"https://ffxivteamcraft.com/db/{Loc.Language}/item/{item.Id}");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Teamcraft");
        ImGui.SameLine();
        if (IconButton.Draw("##google", 0, FontAwesomeIcon.Search, Loc.T("Google-Suche nach dem Item-Namen"), theme))
            Util.OpenLink("https://www.google.com/search?q=" + Uri.EscapeDataString(item.Name + " FFXIV"));
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Google");
    }

    private void DrawSource(ItemSource source, string id)
    {
        // The row is as high as its action button, the text centred in it.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(source.Title);
        ImGui.SameLine();
        ImGui.TextDisabled(source.Detail);
        DrawCost(source, theme);
        DrawAction(source.Action, id, theme);
    }

    /// <summary>The source's cost (currency icon and amount) on the current line, when it has one.</summary>
    public static void DrawCost(ItemSource source, Theme.Theme theme)
    {
        if (source.CostItemId == 0 || source.CostAmount == 0)
            return;
        ImGui.SameLine();
        var costIcon = SpecialShopCost.IsExactCurrency(source.CostItemId) ? SourceIcons.ItemIcon(source.CostItemId) : SourceIcons.IconFor(SourceCategory.Scrips);
        GameIcon.Draw(costIcon, new Vector2(ImGui.GetFrameHeight()), FontAwesomeIcon.Coins, theme.Colors.TextMuted);
        ImGui.SameLine(0, 2);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(source.CostAmount.ToString("N0"));
    }

    /// <summary>The action's button (map, duty finder, recipe, mog station) on the current line, when there is an action.</summary>
    public static void DrawAction(SourceAction? action, string id, Theme.Theme theme)
    {
        if (action == null || SourceActions.ButtonFor(action) is not { } b)
            return;
        ImGui.SameLine();
        if (IconButton.Draw($"##action{id}", b.Icon, b.Fallback, b.Tooltip, theme))
            SourceActions.Run(action);
    }

    private void DrawJobs(GlamourItem item)
    {
        if (item.ClassJobIds.Count >= 40)
        {
            ImGui.TextDisabled(Loc.T("Alle Klassen"));
            return;
        }

        var size = new Vector2(ImGui.GetFrameHeight());
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (size.X + 4)));
        var shown = jobs.All.Where(j => item.ClassJobIds.Contains(j.Id)).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            if (i % perRow != 0)
                ImGui.SameLine(0, 4);
            GameIcon.Draw(shown[i].IconId, size, FontAwesomeIcon.User, theme.Colors.TextMuted);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{shown[i].Abbreviation} – {shown[i].Name}");
        }
    }

    private void DrawCalibration(GlamourItem item)
    {
        var contentId = Plugin.PlayerState.ContentId;
        var slotName = SlotNames.All.FirstOrDefault(s => s.Slot == item.Slot).Name ?? item.Slot.ToString();

        if (IconButton.Draw("##calibrate", 0, FontAwesomeIcon.Camera, Loc.F("Kamera-Position festlegen für ({0})", slotName), theme)
            && live.CaptureCamera() is { } preset)
        {
            calibration.Set(contentId, item.Slot, preset);
            state.InvalidateTiles();
        }

        var calibrated = calibration.Get(contentId, item.Slot) != null;
        if (calibrated)
        {
            ImGui.SameLine();
            if (IconButton.Draw("##calibration-reset", 0, FontAwesomeIcon.Undo, Loc.F("Kamera-Position für ({0}) zurück auf automatisch", slotName), theme)
                && calibration.Remove(contentId, item.Slot))
                state.InvalidateTiles();
        }
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        // Without an own calibration the camera frames the slot by the character's measured body.
        ImGui.TextDisabled(calibrated ? Loc.F("{0}: kalibriert", slotName) : Loc.F("{0}: automatisch", slotName));
    }

    public static void DrawLocations(OwnershipService ownership, GlamourItem item)
    {
        var locations = ownership.Current?.LocationsOf(item.Id) ?? [];
        if (locations.Count == 0)
        {
            ImGui.TextDisabled(Loc.T("Nicht im Besitz"));
            return;
        }
        foreach (var l in locations)
            ImGui.TextUnformatted(Loc.F("Fundort: {0} (Stand {1:dd.MM. HH:mm})", l.DisplayName, l.Seen));
    }

    /// <summary>Dye channels and tradability with icons, matching the tile's trait icons.</summary>
    private void DrawTraits(GlamourItem item)
    {
        var colors = theme.Colors;
        var size = new Vector2(ImGui.GetFrameHeight());
        GameIcon.Draw(item.DyeCount > 0 ? TraitIcons.Dye : 0, size, FontAwesomeIcon.PaintBrush, colors.TextMuted);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(item.DyeCount > 0 ? Loc.F("Färbbar ({0})", DyeText(item.DyeCount)) : Loc.T("Nicht färbbar"));
        ImGui.SameLine();
        var (tradeIcon, glyph, text) = item.Trade switch
        {
            Tradability.Marketable => (TraitIcons.Marketable, FontAwesomeIcon.Store, Loc.T("Marktbrett")),
            Tradability.Tradable => (TraitIcons.Tradable, FontAwesomeIcon.ExchangeAlt, Loc.T("Handelbar")),
            _ => (0u, FontAwesomeIcon.Lock, Loc.T("Gebunden")),
        };
        ImGui.BeginGroup();
        GameIcon.Draw(tradeIcon, size, glyph, colors.TextMuted);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);
        ImGui.EndGroup();
        if (item.Trade == Tradability.Marketable)
            DrawMarketHover(item);
    }

    /// <summary>Over the market trait: the cheapest listing on the home world and on its data center (Universalis); a click opens the item there.</summary>
    private void DrawMarketHover(GlamourItem item)
    {
        if (!ImGui.IsItemHovered())
            return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            Util.OpenLink($"https://universalis.app/market/{item.Id}");

        MarketTooltip.Draw(market, item.Id, Loc.T("Klick: auf Universalis öffnen"));
    }

    private static string DyeText(int count) => count switch
    {
        0 => Loc.T("nicht färbbar"),
        1 => Loc.T("1 Farbkanal"),
        _ => Loc.F("{0} Farbkanäle", count),
    };
}
