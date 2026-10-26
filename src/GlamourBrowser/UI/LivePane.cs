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
using GlamourBrowser.Core.Market;

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
    private readonly WishlistService wishlist;
    private readonly CustomSetService customSets;
    private readonly DyeCatalog dyes;
    private readonly CustomSetEditor editor;
    private readonly SetEditorPane editorPane;
    private readonly GlamourPlateBridge plates;
    private readonly TryOnService tryOn;
    private readonly MarketService market;
    private readonly Configuration config;
    // When the try-on was last saved as a set, for a short confirmation next to the button.
    private DateTime? savedAt;
    private readonly HashSet<(uint Item, SourceCategory Category)> expanded = new();
    // Pieces of the selected set whose sources are open.
    private readonly HashSet<uint> openPieces = new();
    private const int SourcesPerCategory = 3;
    private const float CardFrameScale = 1.4f;

    public LivePane(BrowserState state, OwnershipService ownership, LivePreview live, CalibrationService calibration, JobCatalog jobs, Theme.Theme theme,
                    SourceService sources, WishlistService wishlist, CustomSetService customSets, DyeCatalog dyes, CustomSetEditor editor,
                    SetEditorPane editorPane, GlamourPlateBridge plates, TryOnService tryOn, MarketService market, Configuration config)
    {
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
            if (ImGui.Button(Loc.T("Als Set speichern")) && store != null)
            {
                store.Add(CustomSetStore.DefaultName, tryOn.TryOn.Pieces, DateTimeOffset.Now);
                savedAt = DateTime.UtcNow;
            }
        }
        if (savedAt is { } at && DateTime.UtcNow - at < TimeSpan.FromSeconds(3))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Loc.F("Als ‚{0}‘ gespeichert", CustomSetStore.DefaultName));
        }

        DrawWornPieces();
    }

    /// <summary>
    /// One icon per piece the try-on wears (known items, in slot order): a click selects it again, for its card, dyes and
    /// camera, without putting anything on; a right-click takes it off (the main hand aside). Wraps when the pane is narrow.
    /// </summary>
    private void DrawWornPieces()
    {
        var size = new Vector2(ImGui.GetFrameHeight() * 2);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var first = true;
        foreach (var piece in tryOn.TryOn.Pieces)
        {
            if (state.ItemSubjectFor(piece.ItemId) is not { } subject)
                continue;
            if (!first)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + size.X > right)
                    ImGui.NewLine();
            }
            first = false;
            var selected = state.Selected is ItemSubject { Item.Id: var id } && id == piece.ItemId;
            var hint = piece.Slot == EquipSlot.MainHand ? Loc.T("Klick: auswählen") : Loc.T("Klick: auswählen\nRechtsklick: ausziehen");
            if (IconToggle.Draw($"##worn{(int)piece.Slot}", subject.Item.IconId, size, selected, $"{SlotNames.Full(piece.Slot)}: {subject.Name}\n" + hint,
                    theme, FontAwesomeIcon.Tshirt))
            {
                state.Selected = subject;
                live.Select(subject);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                tryOn.TryOn.TakeOff(piece.Slot);
        }
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

        // In the view's top-left corner, like the game's character window: the visor works on the helmet whatever is
        // selected, the weapon toggles everywhere but on sets (which always show the weapon sheathed).
        var buttonPos = pos + new Vector2(8, 8);
        var step = new Vector2(0, ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorScreenPos(buttonPos);
        if (IconButton.Draw("##visor", 0, FontAwesomeIcon.HardHat, live.VisorClosed ? Loc.T("Visier öffnen") : Loc.T("Visier schließen (nur Helme mit Visier)"), theme))
            live.VisorClosed = !live.VisorClosed;
        if (state.Selected is not SetSubject)
        {
            ImGui.SetCursorScreenPos(buttonPos += step);
            if (IconButton.Draw("##draw", 0, FontAwesomeIcon.FistRaised, live.WeaponDrawn ? Loc.T("Waffe wegstecken") : Loc.T("Waffe ziehen"), theme))
                live.WeaponDrawn = !live.WeaponDrawn;
            ImGui.SetCursorScreenPos(buttonPos + step);
            if (IconButton.Draw("##weaponvisible", 0, live.WeaponVisible ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye,
                    live.WeaponVisible ? Loc.T("Waffe ausblenden") : Loc.T("Waffe anzeigen"), theme))
                live.WeaponVisible = !live.WeaponVisible;
        }

        // Bottom-right: how fast dragging and the wheel move the view, at hand where it is used.
        ImGui.SetCursorScreenPos(pos + size - new Vector2(ImGui.GetFrameHeight() + 8, ImGui.GetFrameHeight() + 8));
        if (IconButton.Draw("##livesettings", 0, FontAwesomeIcon.Cog, Loc.T("Empfindlichkeit der Live-Ansicht"), theme))
            ImGui.OpenPopup("##livesensitivity");
        DrawSensitivityPopup();
        ImGui.SetCursorScreenPos(origin + new Vector2(0, area.Y + ImGui.GetStyle().ItemSpacing.Y));
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
        if (state.Selected is not ItemSubject { Item: var item })
        {
            ImGui.TextDisabled(Loc.T("Kein Item ausgewählt"));
            return;
        }

        var colors = theme.Colors;
        // The dye fields sit in the top-right corner; name and level wrap before them.
        var topRight = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X, 0);
        var wrapAt = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - DyeBoxesWidth(item) - ImGui.GetStyle().ItemSpacing.X;
        GameIcon.Draw(item.IconId, new Vector2(40 * UiScale.Global), FontAwesomeIcon.Tshirt, colors.TextMuted);
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

        DrawTraits(item);
        ImGui.Spacing();
        DrawJobs(item);
        ImGui.Spacing();
        DrawLocations(ownership, item);

        ImGui.Separator();
        if (IconButton.Draw("##tryon", 0, FontAwesomeIcon.Tshirt, Loc.T("Anprobe"), theme))
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
        if (IconButton.Draw("##showingrid", 0, FontAwesomeIcon.Crosshairs, Loc.T("In der Slot-Auswahl zeigen: springt zur Seite mit diesem Item"), theme))
            state.ShowInGrid(item);
        ImGui.SameLine();
        DrawCalibration(item);

        ImGui.Separator();
        DrawSources(item);
        DrawLinks(item, atBottom: true);
    }

    /// <summary>Name, progress and origin of a set, then its pieces: owned or not, where they are or come from; a click opens a piece's sources.</summary>
    private void DrawSetCard(SetSubject set)
    {
        var colors = theme.Colors;
        bool IsOwned(uint id) => ownership.Current?.IsOwned(id) ?? false;
        var progress = SetProgress.Of(set, IsOwned);
        var body = set.Pieces.FirstOrDefault(p => p.Slot == EquipSlot.Body) ?? set.Pieces[0];
        GameIcon.Draw(body.IconId, new Vector2(40 * UiScale.Global), FontAwesomeIcon.LayerGroup, colors.TextMuted);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(set.Name);
        var origin = set.Set.Id.Kind switch
        {
            SetKind.Dresser => Loc.T("Kommoden-Set"),
            SetKind.MogStation => Loc.T("Mogstation-Set"),
            SetKind.Custom => Loc.T("Eigenes Set"),
            _ => Loc.T("Ausrüstungs-Set"),
        };
        ImGui.TextDisabled($"{progress.Owned}/{progress.Total} · {origin}");
        ImGui.EndGroup();
        DrawWishToggle(set);
        if (set.Set.Id.Kind == SetKind.Custom)
        {
            ImGui.SameLine();
            if (IconButton.Draw("##editset", 0, FontAwesomeIcon.Pen, Loc.T("Bearbeiten"), theme) && customSets.Current?.Find(set.Set.Id.Value) is { } stored)
                editor.StartEdit(stored);
            ImGui.SameLine();
            DrawDeleteButton(set);
            if (customSets.Current?.Find(set.Set.Id.Value) is { } toTransfer)
                PlateTransferUi.DrawButton(plates, toTransfer.Pieces);
        }
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
    private void DrawDeleteButton(SetSubject set)
    {
        if (IconButton.Draw("##deleteset", 0, FontAwesomeIcon.Trash, Loc.T("Löschen"), theme))
            ImGui.OpenPopup("##deleteset-confirm");
        if (!ImGui.BeginPopup("##deleteset-confirm"))
            return;
        ImGui.TextUnformatted(Loc.F("Set „{0}“ löschen?", set.Name));
        if (ImGui.Button(Loc.T("Löschen")))
        {
            customSets.Current?.Remove(set.Set.Id.Value);
            state.Selected = null;
            live.Select(null);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Abbrechen")))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

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
            Util.OpenLink($"https://ffxivteamcraft.com/db/de/item/{item.Id}");
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

    /// <summary>The star that puts the item or set on the wishlist (accent frame while listed); grayed out while logged out.</summary>
    private void DrawWishToggle(TileSubject? subject)
    {
        if (subject == null)
            return;
        var loggedIn = wishlist.Current != null;
        var listed = wishlist.IsListed(subject);
        var tooltip = !loggedIn ? wishlist.LoadFailed ? Loc.T("Wunschliste konnte nicht geladen werden") : Loc.T("Nicht eingeloggt")
                    : listed ? Loc.T("Von der Wunschliste nehmen") : Loc.T("Auf die Wunschliste");
        if (IconToggle.Draw("##wish", SlotIcons.Wishlist, new Vector2(ImGui.GetFrameHeight()), listed, tooltip, theme, FontAwesomeIcon.Star, enabled: loggedIn))
            wishlist.Toggle(subject);
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
            if (IconButton.Draw("##calibration-reset", 0, FontAwesomeIcon.Undo, Loc.F("Kamera-Position für ({0}) zurücksetzen", slotName), theme)
                && calibration.Remove(contentId, item.Slot))
                state.InvalidateTiles();
        }
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(calibrated ? Loc.F("{0}: kalibriert", slotName) : Loc.F("{0}: Standardansicht", slotName));
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

        using var tooltip = ImRaii.Tooltip();
        ImGui.TextUnformatted("Universalis");
        if (!Plugin.PlayerState.IsLoaded || !Plugin.PlayerState.HomeWorld.IsValid)
        {
            ImGui.TextDisabled(Loc.T("Preise erst nach dem Einloggen (Heimatwelt)"));
        }
        else
        {
            var home = Plugin.PlayerState.HomeWorld.Value;
            var world = home.Name.ExtractText();
            var dataCenter = home.DataCenter.Value.Name.ExtractText();
            ImGui.TextUnformatted(world + ": " + MarketText(market.Get(item.Id, world), withWorld: false));
            ImGui.TextUnformatted(Loc.F("{0}, günstigstes", dataCenter) + ": " + MarketText(market.Get(item.Id, dataCenter), withWorld: true));
        }
        ImGui.Spacing();
        ImGui.TextDisabled(Loc.T("Klick: auf Universalis öffnen"));
    }

    private static string MarketText(MarketAnswer answer, bool withWorld) => answer switch
    {
        { State: MarketState.Loading } => Loc.T("lädt …"),
        { State: MarketState.Failed } => Loc.T("nicht erreichbar"),
        { Price: null } => Loc.T("keine Angebote"),
        { Price: { } p } => Loc.F("{0} Gil", p.PricePerUnit.ToString("N0", Loc.Culture))
                            + (p.Hq ? " HQ" : "")
                            + (withWorld && p.World != null ? " " + Loc.F("auf {0}", p.World) : "")
                            + " (" + Age(p.Updated) + ")",
    };

    private static string Age(DateTimeOffset updated)
    {
        var age = DateTimeOffset.UtcNow - updated;
        if (age.TotalHours < 1)
            return Loc.F("vor {0} Min.", Math.Max(1, (int)age.TotalMinutes));
        if (age.TotalDays < 1)
            return Loc.F("vor {0} Std.", (int)age.TotalHours);
        return Loc.F("vor {0} Tagen", (int)age.TotalDays);
    }

    private static string DyeText(int count) => count switch
    {
        0 => Loc.T("nicht färbbar"),
        1 => Loc.T("1 Farbkanal"),
        _ => Loc.F("{0} Farbkanäle", count),
    };
}
