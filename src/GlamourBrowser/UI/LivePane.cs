using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using Dalamud.Utility;
using GlamourBrowser.Sources;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;
using GlamourBrowser.GlamourPlate;

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
    private readonly HashSet<(uint Item, SourceCategory Category)> expanded = new();
    // Pieces of the selected set whose sources are open.
    private readonly HashSet<uint> openPieces = new();
    private const int SourcesPerCategory = 3;

    public LivePane(BrowserState state, OwnershipService ownership, LivePreview live, CalibrationService calibration, JobCatalog jobs, Theme.Theme theme,
                    SourceService sources, WishlistService wishlist, CustomSetService customSets, DyeCatalog dyes, CustomSetEditor editor,
                    SetEditorPane editorPane, GlamourPlateBridge plates)
    {
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

        ImGui.Spacing();
        using var card = ImRaii.Child("##itemcard", Vector2.Zero, true);
        if (!card.Success)
            return;
        PlateTransferUi.DrawResult(plates);
        // While a custom set is edited, the editor takes the card's place.
        if (editor.IsOpen)
            editorPane.Draw();
        else
            DrawItemCard();
    }

    private void DrawLive(Vector2 area)
    {
        var colors = theme.Colors;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var texture = live.Texture;

        if (state.Selected == null || texture == null || texture->D3D11ShaderResourceView == null)
        {
            ImGui.Dummy(area);
            drawList.AddRectFilled(origin, origin + area, colors.Card, 8f);
            var hint = state.Selected == null ? "Item im Raster wählen" : "Live-Ansicht wird vorbereitet …";
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
        // The invisible button captures mouse drags so the window does not move.
        ImGui.InvisibleButton("##liveview", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
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
        ImGui.SetCursorScreenPos(origin + new Vector2(0, area.Y + ImGui.GetStyle().ItemSpacing.Y));
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
            ImGui.SetTooltip("Links ziehen: drehen\nRechts ziehen: verschieben\nMausrad: Zoom\nDoppelklick: zurücksetzen");
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
            ImGui.TextDisabled("Kein Item ausgewählt");
            return;
        }

        var colors = theme.Colors;
        GameIcon.Draw(item.IconId, new Vector2(40 * UiScale.Global), FontAwesomeIcon.Tshirt, colors.TextMuted);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(item.Name);
        var patch = sources.IsReady ? sources.Index.PatchOf(item.Id) : 0;
        ImGui.TextDisabled($"Level {item.LevelEquip}" + (patch > 0 ? $" · Patch {patch:0.0#}" : ""));
        ImGui.EndGroup();

        DrawTraits(item);

        DrawJobs(item);
        DrawLocations(ownership, item);

        ImGui.Separator();
        DrawWishToggle(state.Selected);
        ImGui.SameLine();
        if (IconButton.Draw("##tryon", 0, FontAwesomeIcon.Tshirt, "Anprobe", theme))
            Plugin.Framework.RunOnFrameworkThread(() => AgentTryon.TryOn(0, item.Id));
        ImGui.SameLine();
        if (IconButton.Draw("##chatlink", 0, FontAwesomeIcon.CommentDots, "Chat-Link: schreibt einen Item-Link in deinen eigenen Chat und kopiert den Namen.", theme))
        {
            Plugin.ChatGui.Print(new SeStringBuilder().AddText("GlamourBrowser: ").AddItemLink(item.Id, false).Build());
            ImGui.SetClipboardText(item.Name);
        }
        ImGui.SameLine();
        if (IconButton.Draw("##draw", 0, FontAwesomeIcon.FistRaised, live.WeaponDrawn ? "Waffe wegstecken" : "Waffe ziehen", theme))
            live.WeaponDrawn = !live.WeaponDrawn;
        ImGui.SameLine();
        if (IconButton.Draw("##weaponvisible", 0, live.WeaponVisible ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye,
                live.WeaponVisible ? "Waffe ausblenden" : "Waffe anzeigen", theme))
            live.WeaponVisible = !live.WeaponVisible;
        ImGui.SameLine();
        DrawCalibration(item);

        ImGui.Separator();
        DrawSources(item);
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
            SetKind.Dresser => "Kommoden-Set",
            SetKind.MogStation => "Mogstation-Set",
            SetKind.Custom => "Eigenes Set",
            _ => "Ausrüstungs-Set",
        };
        ImGui.TextDisabled($"{progress.Owned}/{progress.Total} · {origin}");
        ImGui.EndGroup();
        DrawWishToggle(set);
        if (set.Set.Id.Kind == SetKind.Custom)
        {
            ImGui.SameLine();
            if (IconButton.Draw("##editset", 0, FontAwesomeIcon.Pen, "Bearbeiten", theme) && customSets.Current?.Find(set.Set.Id.Value) is { } stored)
                editor.StartEdit(stored);
            ImGui.SameLine();
            DrawDeleteButton(set);
            if (customSets.Current?.Find(set.Set.Id.Value) is { } toTransfer)
                PlateTransferUi.DrawButton(plates, toTransfer.Pieces);
        }
        ImGui.Separator();

        var iconSize = new Vector2(ImGui.GetTextLineHeight());
        foreach (var piece in set.Pieces)
        {
            var owned = IsOwned(piece.Id);
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
                DrawSources(piece);
            ImGui.Separator();
        }
    }

    /// <summary>A small dot per dye of the slot (custom sets), the dye's name as tooltip.</summary>
    private void DrawDyeDots(SetSubject set, EquipSlot slot)
    {
        var (stain0, stain1) = set.StainsOf(slot);
        foreach (var stain in new[] { stain0, stain1 })
        {
            if (stain == 0)
                continue;
            ImGui.SameLine();
            var line = ImGui.GetTextLineHeight();
            var radius = line * 0.35f;
            var min = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(radius * 2, line));
            ImGui.GetWindowDrawList().AddCircleFilled(min + new Vector2(radius, line / 2), radius, dyes.Find(stain)?.Color ?? theme.Colors.TextMuted);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(dyes.NameOf(stain));
        }
    }

    /// <summary>Deletes a custom set after asking.</summary>
    private void DrawDeleteButton(SetSubject set)
    {
        if (IconButton.Draw("##deleteset", 0, FontAwesomeIcon.Trash, "Löschen", theme))
            ImGui.OpenPopup("##deleteset-confirm");
        if (!ImGui.BeginPopup("##deleteset-confirm"))
            return;
        ImGui.TextUnformatted($"Set „{set.Name}“ löschen?");
        if (ImGui.Button("Löschen"))
        {
            customSets.Current?.Remove(set.Set.Id.Value);
            state.Selected = null;
            live.Select(null);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Abbrechen"))
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
        ImGui.TextUnformatted("Quellen");
        if (!sources.IsReady)
        {
            ImGui.TextDisabled("Quellen werden geladen …");
            return;
        }

        var all = sources.Index.SourcesOf(item.Id);
        if (all.Count == 0)
            ImGui.TextDisabled("Keine Quelle bekannt");

        var iconSize = new Vector2(ImGui.GetTextLineHeight());
        foreach (var group in all.GroupBy(s => s.Category).OrderBy(g => g.Key))
        {
            var iconMin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(iconSize);
            TileGrid.DrawSourceIcon(ImGui.GetWindowDrawList(), group.Key, iconMin, iconSize.X, theme.Colors.TextMuted);
            ImGui.SameLine();
            ImGui.TextUnformatted(SourceCategories.DisplayName(group.Key));

            var key = (item.Id, group.Key);
            var list = group.ToList();
            var shown = expanded.Contains(key) ? list : list.Take(SourcesPerCategory).ToList();
            using (ImRaii.PushIndent(iconSize.X + ImGui.GetStyle().ItemSpacing.X))
            {
                for (var i = 0; i < shown.Count; i++)
                    DrawSource(shown[i], $"{group.Key}-{i}");

                if (list.Count > SourcesPerCategory && !expanded.Contains(key) && ImGui.SmallButton($"weitere ({list.Count - SourcesPerCategory}) …##more{group.Key}"))
                    expanded.Add(key);
            }
        }

        ImGui.Spacing();
        if (IconButton.Draw("##garland", 0, FontAwesomeIcon.ExternalLinkAlt, "Garland Tools", theme))
            Util.OpenLink($"https://www.garlandtools.org/db/#item/{item.Id}");
        ImGui.SameLine();
        ImGui.TextDisabled("Garland");
        ImGui.SameLine();
        if (IconButton.Draw("##teamcraft", 0, FontAwesomeIcon.ExternalLinkAlt, "Teamcraft", theme))
            Util.OpenLink($"https://ffxivteamcraft.com/db/de/item/{item.Id}");
        ImGui.SameLine();
        ImGui.TextDisabled("Teamcraft");
    }

    private void DrawSource(ItemSource source, string id)
    {
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
        GameIcon.Draw(costIcon, new Vector2(ImGui.GetTextLineHeight()), FontAwesomeIcon.Coins, theme.Colors.TextMuted);
        ImGui.SameLine(0, 2);
        ImGui.TextUnformatted(source.CostAmount.ToString("N0"));
    }

    /// <summary>The action's button (map, duty finder, recipe, mog station) on the current line, when there is an action.</summary>
    public static void DrawAction(SourceAction? action, string id, Theme.Theme theme)
    {
        if (action == null || SourceActions.ButtonFor(action) is not { } b)
            return;
        ImGui.SameLine();
        if (IconButton.Draw($"##action{id}", b.Icon, b.Fallback, b.Tooltip, theme, ImGui.GetTextLineHeight() + 4))
            SourceActions.Run(action);
    }

    /// <summary>The star that puts the item or set on the wishlist (accent frame while listed); grayed out while logged out.</summary>
    private void DrawWishToggle(TileSubject? subject)
    {
        if (subject == null)
            return;
        var loggedIn = wishlist.Current != null;
        var listed = wishlist.IsListed(subject);
        var tooltip = !loggedIn ? wishlist.LoadFailed ? "Wunschliste konnte nicht geladen werden" : "Nicht eingeloggt"
                    : listed ? "Von der Wunschliste nehmen" : "Auf die Wunschliste";
        if (IconToggle.Draw("##wish", SlotIcons.Wishlist, new Vector2(ImGui.GetFrameHeight()), listed, tooltip, theme, FontAwesomeIcon.Star, enabled: loggedIn))
            wishlist.Toggle(subject);
    }

    private void DrawJobs(GlamourItem item)
    {
        if (item.ClassJobIds.Count >= 40)
        {
            ImGui.TextDisabled("Alle Klassen");
            return;
        }

        var size = new Vector2(20 * UiScale.Global);
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

        if (IconButton.Draw("##calibrate", 0, FontAwesomeIcon.Camera, $"Kamera-Position festlegen für ({slotName})", theme)
            && live.CaptureCamera() is { } preset)
        {
            calibration.Set(contentId, item.Slot, preset);
            state.InvalidateTiles();
        }

        var calibrated = calibration.Get(contentId, item.Slot) != null;
        if (calibrated)
        {
            ImGui.SameLine();
            if (IconButton.Draw("##calibration-reset", 0, FontAwesomeIcon.Undo, $"Kamera-Position für ({slotName}) zurücksetzen", theme)
                && calibration.Remove(contentId, item.Slot))
                state.InvalidateTiles();
        }
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(calibrated ? $"{slotName}: kalibriert" : $"{slotName}: Standardansicht");
    }

    public static void DrawLocations(OwnershipService ownership, GlamourItem item)
    {
        var locations = ownership.Current?.LocationsOf(item.Id) ?? [];
        if (locations.Count == 0)
        {
            ImGui.TextDisabled("Nicht im Besitz");
            return;
        }
        foreach (var l in locations)
            ImGui.TextUnformatted($"Fundort: {l.DisplayName} (Stand {l.Seen:dd.MM. HH:mm})");
    }

    /// <summary>Dye channels and tradability with icons, matching the tile's trait icons.</summary>
    private void DrawTraits(GlamourItem item)
    {
        var colors = theme.Colors;
        var size = new Vector2(ImGui.GetTextLineHeight());
        GameIcon.Draw(item.DyeCount > 0 ? TraitIcons.Dye : 0, size, FontAwesomeIcon.PaintBrush, colors.TextMuted);
        ImGui.SameLine();
        ImGui.TextUnformatted(item.DyeCount > 0 ? $"Färbbar ({DyeText(item.DyeCount)})" : "Nicht färbbar");
        ImGui.SameLine();
        var (tradeIcon, glyph, text) = item.Trade switch
        {
            Tradability.Marketable => (TraitIcons.Marketable, FontAwesomeIcon.Store, "Marktbrett"),
            Tradability.Tradable => (TraitIcons.Tradable, FontAwesomeIcon.ExchangeAlt, "Handelbar"),
            _ => (0u, FontAwesomeIcon.Lock, "Gebunden"),
        };
        GameIcon.Draw(tradeIcon, size, glyph, colors.TextMuted);
        ImGui.SameLine();
        ImGui.TextUnformatted(text);
    }

    private static string DyeText(int count) => count switch
    {
        0 => "nicht färbbar",
        1 => "1 Farbkanal",
        _ => $"{count} Farbkanäle",
    };
}
