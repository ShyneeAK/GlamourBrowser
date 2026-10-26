using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.CustomSets;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>
/// The folders of "Meine Sets": their tiles (a collage of the first sets, a click opens it), "+ Neuer Ordner", the way back,
/// renaming and deleting, and moving sets in by dragging their tile or by the tile's right-click menu.
/// </summary>
public sealed class MySetsFolders(BrowserState state, CustomSetService customSets, TileCache<IDalamudTextureWrap> cache, Theme.Theme theme)
{
    private const string DragType = "GB_CUSTOMSET";
    private const string FolderPopup = "##foldercontext";
    private const string NamePopup = "##foldername";

    // The custom set being dragged (the payload carries no data).
    private uint draggedSet;
    private CustomFolder? contextFolder;
    // The folder being renamed; null while a new one is named.
    private CustomFolder? renaming;
    private string nameInput = "";
    private bool openNamePopup;

    /// <summary>Before "+ Neues Set": in an open folder the way back and its name, at the top "+ Neuer Ordner".</summary>
    public void DrawHeader(CustomSetStore store)
    {
        if (state.OpenFolder != 0 && store.FindFolder(state.OpenFolder) is { } open)
        {
            if (ImGui.Button("← " + Loc.T("Meine Sets")))
                state.CloseFolder();
            DropTarget(0);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Loc.T("Zurück zu allen Sets; ein Set hierher ziehen nimmt es aus dem Ordner"));
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Colors.Accent))
            using (UiScale.PushIcons())
                ImGui.TextUnformatted(FontAwesomeIcon.FolderOpen.ToIconString());
            ImGui.SameLine();
            ImGui.TextUnformatted(open.Name);
            ImGui.SameLine();
            if (IconButton.Draw("##renamefolder", 0, FontAwesomeIcon.Pen, Loc.T("Ordner umbenennen"), theme))
                StartNaming(open);
            ImGui.SameLine();
        }
        else
        {
            if (ImGui.Button(Loc.T("+ Neuer Ordner")))
                StartNaming(null);
            ImGui.SameLine();
        }
    }

    /// <summary>The popups: naming a folder and a folder tile's right-click menu; once per frame, after the tiles.</summary>
    public void DrawPopups()
    {
        if (openNamePopup)
        {
            ImGui.OpenPopup(NamePopup);
            openNamePopup = false;
        }
        if (ImGui.BeginPopup(NamePopup))
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            var done = ImGui.InputTextWithHint("##name", CustomSetStore.DefaultFolderName, ref nameInput, CustomSetStore.MaxNameLength * 4 + 1,
                                               ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            done |= ImGui.Button(renaming == null ? Loc.T("Anlegen") : Loc.T("Umbenennen"));
            if (done && customSets.Current is { } store)
            {
                if (renaming == null)
                    store.AddFolder(nameInput);
                else
                    store.RenameFolder(renaming.Id, nameInput);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup(FolderPopup))
        {
            if (contextFolder is { } folder)
            {
                ImGui.TextDisabled(folder.Name);
                if (ImGui.MenuItem(Loc.T("Öffnen")))
                    state.ShowFolder(folder.Id);
                if (ImGui.MenuItem(Loc.T("Ordner umbenennen")))
                    StartNaming(folder);
                if (ImGui.MenuItem(Loc.T("Ordner löschen (die Sets bleiben)")))
                    customSets.Current?.RemoveFolder(folder.Id);
            }
            ImGui.EndPopup();
        }
    }

    /// <summary>A folder's tile with an accent frame: its first four sets as a 2×2 collage (a folder glyph while empty), its name and number of sets.</summary>
    public void DrawTile(FolderSubject folder, Vector2 size, bool dimmed)
    {
        const float rounding = 8f;
        var colors = theme.Colors;
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton($"##folder{folder.Folder.Id}", size))
            state.ShowFolder(folder.Folder.Id);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            contextFolder = folder.Folder;
            ImGui.OpenPopup(FolderPopup);
        }
        var dropping = DropTarget(folder.Folder.Id);
        if (hovered && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetTooltip(Loc.T("Klick: Ordner öffnen\nRechtsklick: umbenennen, löschen\nSet hierher ziehen: hineinlegen"));

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, hovered || dropping ? colors.CardRaised : colors.Card, rounding);

        // The first sets as a 2×2 collage above the name strip; an empty folder shows a large folder glyph.
        var lineHeight = ImGui.GetTextLineHeight();
        var stripHeight = lineHeight * 2.2f + 4;
        const float gap = 4f;
        var areaMin = pos + new Vector2(gap + 2);
        var areaMax = pos + size - new Vector2(gap + 2, stripHeight + gap);
        if (folder.Covers.Count == 0)
        {
            using (UiScale.PushIcons())
            {
                var glyph = FontAwesomeIcon.Folder.ToIconString();
                var scale = MathF.Min(size.X, size.Y) * 0.35f / ImGui.GetFontSize();
                var glyphSize = ImGui.CalcTextSize(glyph) * scale;
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale, (areaMin + areaMax - glyphSize) / 2, colors.TextMuted, glyph);
            }
        }
        else
        {
            var cell = (areaMax - areaMin - new Vector2(gap)) / 2;
            for (var i = 0; i < FolderSubject.MaxCovers; i++)
            {
                var min = areaMin + new Vector2(i % 2, i / 2) * (cell + new Vector2(gap));
                if (i < folder.Covers.Count)
                    DrawCover(drawList, folder.Covers[i], min, cell, size, dimmed);
                else
                    drawList.AddRectFilled(min, min + cell, colors.CardRaised, 5f);
            }
        }

        // The folder glyph and name on the dark strip along the bottom, like the set tiles.
        var shadeTop = pos + new Vector2(0, size.Y - stripHeight);
        drawList.PushClipRect(pos, pos + size, true);
        drawList.AddRectFilled(shadeTop, pos + size, 0xB0000000, rounding, ImDrawFlags.RoundCornersBottom);
        string icon;
        Vector2 iconSize;
        using (UiScale.PushIcons())
        {
            icon = FontAwesomeIcon.Folder.ToIconString();
            iconSize = ImGui.CalcTextSize(icon);
            drawList.AddText(shadeTop + new Vector2(6, 4), colors.Accent, icon);
        }
        var textX = 6 + iconSize.X + 6;
        drawList.AddText(shadeTop + new Vector2(textX, 4), colors.Text, folder.Name);
        var count = folder.Sets.Count == 1 ? Loc.T("1 Set") : Loc.F("{0} Sets", folder.Sets.Count);
        drawList.AddText(shadeTop + new Vector2(textX, 4 + lineHeight), colors.TextMuted, count);
        drawList.PopClipRect();

        // Always the accent frame: a folder stands out from the set tiles; thicker while a set is dragged over it.
        drawList.AddRect(pos, pos + size, colors.Accent, rounding, ImDrawFlags.None, dropping ? 3f : 2f);
    }

    /// <summary>
    /// One set of the collage: the middle of its render cut to the cell (the render has the tile's shape), or its body
    /// piece's icon until the render is there.
    /// </summary>
    private void DrawCover(ImDrawListPtr drawList, SetSubject set, Vector2 min, Vector2 cell, Vector2 tile, bool dimmed)
    {
        if (cache.TryGet(state.KeyFor(set), out var texture) && texture != null)
        {
            var (uvMin, uvMax) = CoverUv(tile.X / tile.Y, cell.X / cell.Y);
            drawList.AddImageRounded(texture.Handle, min, min + cell, uvMin, uvMax, dimmed ? 0xFFA0A0A0 : 0xFFFFFFFF, 5f);
            return;
        }
        drawList.AddRectFilled(min, min + cell, theme.Colors.CardRaised, 5f);
        var piece = set.Wear.FirstOrDefault(w => w.Slot == EquipSlot.Body) ?? set.Wear.FirstOrDefault();
        if (piece == null)
            return;
        var edge = new Vector2(MathF.Min(cell.X, cell.Y) * 0.6f);
        GameIcon.DrawAt(drawList, piece.IconId, min + (cell - edge) / 2, edge, FontAwesomeIcon.Tshirt, theme.Colors.TextMuted);
    }

    /// <summary>The part of a render (aspect <paramref name="image"/>) that fills a cell (aspect <paramref name="cell"/>): its middle.</summary>
    private static (Vector2 Min, Vector2 Max) CoverUv(float image, float cell)
    {
        if (cell > image)
        {
            var height = image / cell;
            return (new Vector2(0, (1 - height) / 2), new Vector2(1, (1 + height) / 2));
        }
        var width = cell / image;
        return (new Vector2((1 - width) / 2, 0), new Vector2((1 + width) / 2, 1));
    }

    /// <summary>A custom set's tile can be dragged onto a folder (or the way back); call right after the tile's button.</summary>
    /// <param name="tile">The tile's size: the cursor carries a small copy of it.</param>
    public void DragSource(SetSubject set, Vector2 tile)
    {
        if (set.Set.Id.Kind != SetKind.Custom || customSets.Current is not { Folders.Count: > 0 } && state.OpenFolder == 0)
            return;
        if (!ImGui.BeginDragDropSource())
            return;
        draggedSet = set.Set.Id.Value;
        ImGui.SetDragDropPayload(DragType, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
        DrawMiniature(set, tile);
        ImGui.EndDragDropSource();
    }

    /// <summary>The dragged tile in small: its render (or its body piece's icon) with an accent frame, the name below.</summary>
    private void DrawMiniature(SetSubject set, Vector2 tile)
    {
        var width = MathF.Min(tile.X * 0.5f, 110 * UiScale.Global);
        var size = new Vector2(width, width * tile.Y / MathF.Max(1, tile.X));
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        var drawList = ImGui.GetWindowDrawList();
        if (cache.TryGet(state.KeyFor(set), out var texture) && texture != null)
            drawList.AddImageRounded(texture.Handle, min, min + size, Vector2.Zero, Vector2.One, 0xFFFFFFFF, 6f);
        else
        {
            drawList.AddRectFilled(min, min + size, theme.Colors.CardRaised, 6f);
            if ((set.Wear.FirstOrDefault(w => w.Slot == EquipSlot.Body) ?? set.Wear.FirstOrDefault()) is { } piece)
            {
                var edge = new Vector2(width * 0.5f);
                GameIcon.DrawAt(drawList, piece.IconId, min + (size - edge) / 2, edge, FontAwesomeIcon.Tshirt, theme.Colors.TextMuted);
            }
        }
        drawList.AddRect(min, min + size, theme.Colors.Accent, 6f, ImDrawFlags.None, 2f);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextWrapped(set.Name);
        ImGui.PopTextWrapPos();
    }

    /// <summary>"In Ordner verschieben" with every folder and "Kein Ordner" for a custom set's right-click menu.</summary>
    public void DrawMoveMenu(SetSubject set)
    {
        if (set.Set.Id.Kind != SetKind.Custom || customSets.Current is not { } store || store.Find(set.Set.Id.Value) is not { } stored)
            return;
        if (store.Folders.Count == 0 && stored.Folder == 0)
            return;
        ImGui.Separator();
        if (!ImGui.BeginMenu(Loc.T("In Ordner verschieben")))
            return;
        if (ImGui.MenuItem(Loc.T("Kein Ordner"), "", stored.Folder == 0))
            store.Move(stored.Id, 0);
        foreach (var folder in store.Folders)
        {
            if (ImGui.MenuItem($"{folder.Name}##folder{folder.Id}", "", stored.Folder == folder.Id))
                store.Move(stored.Id, folder.Id);
        }
        ImGui.EndMenu();
    }

    /// <summary>Takes a dragged custom set into the folder (0: out of any); true while one is dragged over the item.</summary>
    private bool DropTarget(uint folder)
    {
        if (!ImGui.BeginDragDropTarget())
            return false;
        var payload = ImGui.AcceptDragDropPayload(DragType);
        if (!payload.IsNull && payload.IsDelivery())
            customSets.Current?.Move(draggedSet, folder);
        ImGui.EndDragDropTarget();
        return !payload.IsNull;
    }

    private void StartNaming(CustomFolder? folder)
    {
        renaming = folder;
        nameInput = folder?.Name ?? "";
        openNamePopup = true;
    }
}
