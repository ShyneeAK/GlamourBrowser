using System;
using System.Collections.Generic;
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
/// renaming and deleting, and moving sets in by dragging their tile or by the tile's right-click menu, several at once
/// after marking them with Ctrl+click or Shift+click.
/// </summary>
public sealed class MySetsFolders(BrowserState state, CustomSetService customSets, TileCache<IDalamudTextureWrap> cache, Theme.Theme theme)
{
    private const string DragType = "GB_CUSTOMSET";
    private const string FolderPopup = "##foldercontext";
    private const string NamePopup = "##foldername";
    private const string DeletePopup = "##folderdelete";

    // The custom sets being dragged (the payload carries no data).
    private IReadOnlyList<uint> draggedSets = [];
    private readonly SetMarks marks = new();
    // Where the marks were made: another folder, or leaving "Meine Sets" (no header drawn for a frame), drops them.
    private uint marksFolder;
    private int headerFrame;
    private CustomFolder? contextFolder;
    // The folder whose trash glyph was clicked, asked about before it goes.
    private CustomFolder? deleting;
    // The folder being renamed; null while a new one is named.
    private CustomFolder? renaming;
    private string nameInput = "";
    private bool openNamePopup;

    /// <summary>Before "+ Neues Set": in an open folder the way back and its name, at the top "+ Neuer Ordner".</summary>
    public void DrawHeader(CustomSetStore store)
    {
        var frame = ImGui.GetFrameCount();
        if (state.OpenFolder != marksFolder || frame - headerFrame > 1)
        {
            marks.Clear();
            marksFolder = state.OpenFolder;
        }
        headerFrame = frame;

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
        }
        else
        {
            if (ImGui.Button(Loc.T("+ Neuer Ordner")))
                StartNaming(null);
            ImGui.SameLine();
        }
    }

    /// <summary>After the header's buttons: how many sets are marked and a way to drop the marks, or how to mark them.</summary>
    public void DrawMarks(CustomSetStore store)
    {
        if (marks.Count > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(marks.Count == 1 ? Loc.T("1 Set markiert") : Loc.F("{0} Sets markiert", marks.Count));
            ImGui.SameLine();
            if (ImGui.Button(Loc.T("Markierung aufheben")))
                marks.Clear();
        }
        else if (store.Folders.Count > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(Loc.T("Strg+Klick oder Umschalt+Klick: mehrere Sets markieren"));
        }
    }

    public bool IsMarked(TileSubject subject) => subject is SetSubject { Set.Id: { Kind: SetKind.Custom } id } && marks.IsMarked(id.Value);

    /// <summary>
    /// A click on a custom set's tile: with Ctrl it marks the set (or takes the mark off), with Shift the range from the
    /// last one; true then, and the tile does nothing else. A plain click drops the marks and is the tile's own.
    /// </summary>
    public bool Click(SetSubject set)
    {
        if (set.Set.Id.Kind != SetKind.Custom)
            return false;
        var io = ImGui.GetIO();
        if (io.KeyCtrl)
        {
            marks.Toggle(set.Set.Id.Value);
            return true;
        }
        if (io.KeyShift)
        {
            var shown = state.Results.OfType<SetSubject>().Where(s => s.Set.Id.Kind == SetKind.Custom).Select(s => s.Set.Id.Value).ToList();
            marks.MarkRange(shown, set.Set.Id.Value);
            return true;
        }
        marks.Clear();
        return false;
    }

    /// <summary>A marked tile: a thick accent frame and a check in an accent circle at the top, right of the middle.</summary>
    public void DrawMark(ImDrawListPtr drawList, Vector2 pos, Vector2 size)
    {
        var accent = theme.Colors.Accent;
        drawList.AddRect(pos, pos + size, accent, 8f, ImDrawFlags.None, 3f);
        using (UiScale.PushIcons())
        {
            var glyph = FontAwesomeIcon.Check.ToIconString();
            var glyphSize = ImGui.CalcTextSize(glyph);
            var radius = MathF.Max(glyphSize.X, glyphSize.Y) * 0.75f;
            var center = pos + new Vector2(size.X - radius - MathF.Max(10f, size.X * 0.12f) - 6, radius + 6);
            drawList.AddCircleFilled(center, radius, accent);
            drawList.AddText(center - glyphSize / 2, 0xFFFFFFFF, glyph);
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

        if (ImGui.BeginPopup(DeletePopup))
        {
            if (deleting is { } folder)
            {
                ImGui.TextUnformatted(Loc.F("Ordner „{0}“ löschen? Die Sets darin bleiben erhalten.", folder.Name));
                if (ImGui.Button(Loc.T("Löschen")))
                {
                    customSets.Current?.RemoveFolder(folder.Id);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button(Loc.T("Abbrechen")))
                    ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        var askDelete = false;
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
                {
                    deleting = folder;
                    askDelete = true;
                }
            }
            ImGui.EndPopup();
        }
        // Asks first, like the glyph on the tile; opened outside the menu so it lives where the glyph opens it.
        if (askDelete)
            ImGui.OpenPopup(DeletePopup);
    }

    /// <summary>A folder's tile with an accent frame: its first four sets as a 2×2 collage (a folder glyph while empty), its name and number of sets.</summary>
    public void DrawTile(FolderSubject folder, Vector2 size, bool dimmed)
    {
        const float rounding = 8f;
        var colors = theme.Colors;
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##folder{folder.Folder.Id}", size);
        var hovered = ImGui.IsItemHovered();
        // "Umbenennen" and "Löschen" in the top-left corner, where a set tile has its star and its own buttons; decided
        // where the button went down, like there.
        var glyphBox = TileGrid.WishStarSize();
        var renameRect = TileGrid.GlyphRect(pos + new Vector2(6, 5), glyphBox);
        var deleteRect = TileGrid.GlyphRect(pos + new Vector2(6, 5 + glyphBox.Y + 8), glyphBox);
        var overRename = hovered && ImGui.IsMouseHoveringRect(renameRect.Min, renameRect.Max);
        var overDelete = hovered && ImGui.IsMouseHoveringRect(deleteRect.Min, deleteRect.Max);
        var pressed = ImGui.GetIO().MouseClickedPos[0];
        if (clicked && TileGrid.Inside(pressed, renameRect))
            StartNaming(folder.Folder);
        else if (clicked && TileGrid.Inside(pressed, deleteRect))
        {
            deleting = folder.Folder;
            ImGui.OpenPopup(DeletePopup);
        }
        else if (clicked)
            state.ShowFolder(folder.Folder.Id);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            contextFolder = folder.Folder;
            ImGui.OpenPopup(FolderPopup);
        }
        var dropping = DropTarget(folder.Folder.Id);
        if (overRename)
            ImGui.SetTooltip(Loc.T("Ordner umbenennen"));
        else if (overDelete)
            ImGui.SetTooltip(Loc.T("Ordner löschen (die Sets bleiben)"));
        else if (hovered && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetTooltip(Loc.T("Klick: Ordner öffnen\nRechtsklick: umbenennen, löschen\nSet hierher ziehen: hineinlegen"));

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, hovered || dropping ? colors.CardRaised : colors.Card, rounding);

        // The first sets as a 2×2 collage above the name strip; an empty folder shows a large folder glyph.
        var lineHeight = ImGui.GetTextLineHeight();
        // As high as the set tiles' strip, so the names line up; at least one line.
        var stripHeight = MathF.Max(TileGrid.StripHeight(size.Y), lineHeight * 1.2f + 4);
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
        // The folder glyph about three quarters of the strip's height, left of the name and the number of sets.
        float iconWidth;
        using (UiScale.PushIcons())
        {
            var icon = FontAwesomeIcon.Folder.ToIconString();
            var fontSize = (stripHeight - 12) * 0.75f / ImGui.GetTextLineHeight() * ImGui.GetFontSize();
            var iconSize = ImGui.CalcTextSize(icon) * (fontSize / ImGui.GetFontSize());
            iconWidth = iconSize.X;
            drawList.AddText(ImGui.GetFont(), fontSize, shadeTop + new Vector2(8, (stripHeight - iconSize.Y) / 2), colors.Accent, icon);
        }
        var textX = 8 + iconWidth + 8;
        drawList.AddText(shadeTop + new Vector2(textX, 4), colors.Text, folder.Name);
        var count = folder.Sets.Count == 1 ? Loc.T("1 Set") : Loc.F("{0} Sets", folder.Sets.Count);
        drawList.AddText(shadeTop + new Vector2(textX, 4 + lineHeight), colors.TextMuted, count);
        drawList.PopClipRect();

        // Always the accent frame: a folder stands out from the set tiles; thicker while a set is dragged over it.
        drawList.AddRect(pos, pos + size, colors.Accent, rounding, ImDrawFlags.None, dropping ? 3f : 2f);
        if (hovered)
        {
            var muted = (colors.TextMuted & 0x00FFFFFF) | 0xA0000000;
            TileGrid.DrawTileGlyph(drawList, FontAwesomeIcon.Pen, renameRect, overRename ? colors.Text : muted);
            TileGrid.DrawTileGlyph(drawList, FontAwesomeIcon.Trash, deleteRect, overDelete ? colors.Text : muted);
        }
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
    /// Every marked set comes along.
    public void DragSource(SetSubject set, Vector2 tile)
    {
        if (set.Set.Id.Kind != SetKind.Custom || customSets.Current is not { Folders.Count: > 0 } && state.OpenFolder == 0)
            return;
        if (!ImGui.BeginDragDropSource())
            return;
        draggedSets = marks.With(set.Set.Id.Value);
        ImGui.SetDragDropPayload(DragType, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
        DrawMiniature(set, tile, draggedSets.Count);
        ImGui.EndDragDropSource();
    }

    /// <summary>
    /// The dragged tile in small: its render (or its body piece's icon) with an accent frame, the name below; with
    /// several sets two cards peek out behind it and the number replaces the name.
    /// </summary>
    private void DrawMiniature(SetSubject set, Vector2 tile, int count)
    {
        var width = MathF.Min(tile.X * 0.5f, 110 * UiScale.Global);
        var size = new Vector2(width, width * tile.Y / MathF.Max(1, tile.X));
        var stack = count > 1 ? 6f : 0f;
        var min = ImGui.GetCursorScreenPos() + new Vector2(0, stack);
        ImGui.Dummy(size + new Vector2(stack, stack));
        var drawList = ImGui.GetWindowDrawList();
        for (var i = 2; i >= 1 && count > 1; i--)
        {
            var offset = new Vector2(stack * i / 2, -stack * i / 2);
            drawList.AddRectFilled(min + offset, min + offset + size, theme.Colors.CardRaised, 6f);
            drawList.AddRect(min + offset, min + offset + size, theme.Colors.Accent, 6f, ImDrawFlags.None, 1f);
        }
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
        ImGui.TextWrapped(count > 1 ? Loc.F("{0} Sets", count) : set.Name);
        ImGui.PopTextWrapPos();
    }

    /// <summary>"In Ordner verschieben" with every folder and "Kein Ordner" for a custom set's right-click menu.</summary>
    public void DrawMoveMenu(SetSubject set)
    {
        if (set.Set.Id.Kind != SetKind.Custom || customSets.Current is not { } store || store.Find(set.Set.Id.Value) is not { } stored)
            return;
        if (store.Folders.Count == 0 && stored.Folder == 0)
            return;
        // Every marked set moves along.
        var moving = marks.With(stored.Id);
        ImGui.Separator();
        if (!ImGui.BeginMenu(moving.Count > 1 ? Loc.F("{0} Sets in Ordner verschieben", moving.Count) : Loc.T("In Ordner verschieben")))
            return;
        if (ImGui.MenuItem(Loc.T("Kein Ordner"), "", stored.Folder == 0))
            Move(moving, 0);
        foreach (var folder in store.Folders)
        {
            if (ImGui.MenuItem($"{folder.Name}##folder{folder.Id}", "", stored.Folder == folder.Id))
                Move(moving, folder.Id);
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
            Move(draggedSets, folder);
        ImGui.EndDragDropTarget();
        return !payload.IsNull;
    }

    /// <summary>Moves the sets into the folder (0: out of any); the marks go, the moved sets have left the view.</summary>
    private void Move(IReadOnlyList<uint> sets, uint folder)
    {
        if (customSets.Current is not { } store)
            return;
        foreach (var id in sets)
            store.Move(id, folder);
        marks.Clear();
    }

    private void StartNaming(CustomFolder? folder)
    {
        renaming = folder;
        nameInput = folder?.Name ?? "";
        openNamePopup = true;
    }
}
