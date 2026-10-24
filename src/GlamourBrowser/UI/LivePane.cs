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
using GlamourBrowser.Core.Sources;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using GlamourBrowser.Sources;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.UI.Widgets;

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
    private readonly HashSet<(uint Item, SourceCategory Category)> expanded = new();
    private const int SourcesPerCategory = 3;
    private const uint MapIcon = 60561;        // map flag
    private const uint DutyFinderIcon = 46;    // main menu: duty finder
    private const uint RecipeIcon = 22;        // main menu: crafting log

    public LivePane(BrowserState state, OwnershipService ownership, LivePreview live, CalibrationService calibration, JobCatalog jobs, Theme.Theme theme,
                    SourceService sources)
    {
        this.sources = sources;
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
        var slots = state.Filter.Slots;
        if (SlotBar.Draw("slotbar", ref slots, theme))
            state.Filter = state.Filter with { Slots = slots };

        ImGui.Spacing();
        using var card = ImRaii.Child("##itemcard", Vector2.Zero, true);
        if (card.Success)
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
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            glyph = FontAwesomeIcon.QuestionCircle.ToIconString();
            glyphSize = ImGui.CalcTextSize(glyph);
        }

        var min = topRight + new Vector2(-glyphSize.X - 8, 8);
        var hovered = ImGui.IsMouseHoveringRect(min, min + glyphSize) && !ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Right);
        using (ImRaii.PushFont(UiBuilder.IconFont))
            drawList.AddText(min, hovered ? theme.Colors.Text : theme.Colors.TextMuted, glyph);
        if (hovered)
            ImGui.SetTooltip("Links ziehen: drehen\nRechts ziehen: verschieben\nMausrad: Zoom\nDoppelklick: zurücksetzen");
    }

    private void DrawItemCard()
    {
        if (state.Selected is not { } item)
        {
            ImGui.TextDisabled("Kein Item ausgewählt");
            return;
        }

        var colors = theme.Colors;
        GameIcon.Draw(item.IconId, new Vector2(40 * ImGuiHelpers.GlobalScale), FontAwesomeIcon.Tshirt, colors.TextMuted);
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

        if (source.CostItemId != 0 && source.CostAmount > 0)
        {
            ImGui.SameLine();
            var costIcon = SpecialShopCost.IsExactCurrency(source.CostItemId) ? SourceIcons.ItemIcon(source.CostItemId) : SourceIcons.IconFor(SourceCategory.Scrips);
            GameIcon.Draw(costIcon, new Vector2(ImGui.GetTextLineHeight()), FontAwesomeIcon.Coins, theme.Colors.TextMuted);
            ImGui.SameLine(0, 2);
            ImGui.TextUnformatted(source.CostAmount.ToString("N0"));
        }

        (uint Icon, FontAwesomeIcon Fallback, string Tooltip)? button = source.Action switch
        {
            MapFlag => (MapIcon, FontAwesomeIcon.MapMarkerAlt, "Karte"),
            DutyFinder => (DutyFinderIcon, FontAwesomeIcon.Search, "Inhaltssuche"),
            OpenRecipe => (RecipeIcon, FontAwesomeIcon.Book, "Rezept"),
            OpenStore => (0u, FontAwesomeIcon.ShoppingCart, "Mogstation"),
            _ => null,
        };
        if (button is not { } b)
            return;
        ImGui.SameLine();
        if (IconButton.Draw($"##action{id}", b.Icon, b.Fallback, b.Tooltip, theme, ImGui.GetTextLineHeight() + 4))
            Run(source.Action!);
    }

    private static void Run(SourceAction action)
    {
        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                switch (action)
                {
                    case MapFlag flag:
                        Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(flag.TerritoryId, flag.MapId, flag.X, flag.Y, 0.05f));
                        break;
                    case DutyFinder duty:
                        AgentContentsFinder.Instance()->OpenRegularDuty(duty.ContentFinderConditionId);
                        break;
                    case OpenStore store:
                        Util.OpenLink(StoreLinks.ProductUrl(store.StoreId));
                        break;
                    case OpenRecipe recipe:
                        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RecipeId);
                        break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warning(e, $"Source action {action} failed");
            }
        });
    }

    private void DrawJobs(GlamourItem item)
    {
        if (item.ClassJobIds.Count >= 40)
        {
            ImGui.TextDisabled("Alle Klassen");
            return;
        }

        var size = new Vector2(20 * ImGuiHelpers.GlobalScale);
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
        GameIcon.Draw(0, size, FontAwesomeIcon.PaintBrush, item.DyeCount > 0 ? colors.Text : colors.TextMuted);
        ImGui.SameLine();
        ImGui.TextUnformatted(item.DyeCount > 0 ? $"Färbbar ({DyeText(item.DyeCount)})" : "Nicht färbbar");
        ImGui.SameLine();
        var (glyph, text) = item.Trade switch
        {
            Tradability.Marketable => (FontAwesomeIcon.Store, "Marktbrett"),
            Tradability.Tradable => (FontAwesomeIcon.ExchangeAlt, "Handelbar"),
            _ => (FontAwesomeIcon.Lock, "Gebunden"),
        };
        GameIcon.Draw(0, size, glyph, colors.TextMuted);
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
