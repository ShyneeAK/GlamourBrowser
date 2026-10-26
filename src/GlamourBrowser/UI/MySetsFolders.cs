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
/// The folders of "Meine Sets": their tiles (the first set's render, a click opens it), "+ Neuer Ordner", the way back,
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

    /// <summary>A folder's tile: the render of its first set (a folder glyph while empty), its name and number of sets.</summary>
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
        if (folder.Cover is { } cover && cache.TryGet(state.KeyFor(cover), out var texture) && texture != null)
            drawList.AddImageRounded(texture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, dimmed ? 0xFFA0A0A0 : 0xFFFFFFFF, rounding);
        else if (folder.Cover == null)
        {
            using (UiScale.PushIcons())
            {
                var glyph = FontAwesomeIcon.Folder.ToIconString();
                var scale = MathF.Min(size.X, size.Y) * 0.35f / ImGui.GetFontSize();
                var glyphSize = ImGui.CalcTextSize(glyph) * scale;
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale, pos + (size - glyphSize) / 2, colors.TextMuted, glyph);
            }
        }

        // The folder glyph and name on the dark strip along the bottom, like the set tiles.
        var lineHeight = ImGui.GetTextLineHeight();
        var stripHeight = lineHeight * 2.2f + 4;
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

        drawList.AddRect(pos, pos + size, dropping ? colors.Accent : colors.Border, rounding, ImDrawFlags.None, dropping ? 2f : 1f);
    }

    /// <summary>A custom set's tile can be dragged onto a folder (or the way back); call right after the tile's button.</summary>
    public void DragSource(SetSubject set)
    {
        if (set.Set.Id.Kind != SetKind.Custom || customSets.Current is not { Folders.Count: > 0 } && state.OpenFolder == 0)
            return;
        if (!ImGui.BeginDragDropSource())
            return;
        draggedSet = set.Set.Id.Value;
        ImGui.SetDragDropPayload(DragType, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
        ImGui.TextUnformatted(set.Name);
        ImGui.EndDragDropSource();
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
