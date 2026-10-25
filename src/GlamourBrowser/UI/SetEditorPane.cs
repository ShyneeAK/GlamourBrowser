using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.CustomSets;
using GlamourBrowser.Data;
using GlamourBrowser.GlamourPlate;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>
/// The set editor in the left column: name, one row per slot (slot, item, two dyes, remove) and save / discard. A click
/// on a row filters the grid to that slot; a click on a tile then puts the item in.
/// </summary>
public sealed class SetEditorPane
{
    private readonly CustomSetEditor editor;
    private readonly BrowserState state;
    private readonly DyeCatalog dyes;
    private readonly Theme.Theme theme;
    private readonly GlamourPlateBridge plates;
    // The discard question was opened: when it is closed by a click outside, the answer is "keep".
    private bool discardAsked;

    public SetEditorPane(CustomSetEditor editor, BrowserState state, DyeCatalog dyes, Theme.Theme theme, GlamourPlateBridge plates)
    {
        this.plates = plates;
        this.editor = editor;
        this.state = state;
        this.dyes = dyes;
        this.theme = theme;
    }

    public void Draw()
    {
        if (editor.Draft is not { } draft)
            return;

        var name = draft.Name;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##setname", CustomSetStore.DefaultName, ref name, CustomSetStore.MaxNameLength))
            draft.Name = name;
        ImGui.Separator();

        foreach (var slot in CustomSetSubjects.Slots)
        {
            using var id = ImRaii.PushId((int)slot);
            DrawSlot(draft, slot);
        }

        ImGui.Separator();
        using (ImRaii.Disabled(draft.Pieces.Count == 0))
        {
            if (ImGui.Button("Speichern"))
                editor.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Verwerfen"))
            editor.Discard();
        ImGui.SameLine();
        PlateTransferUi.DrawButton(plates, draft.Pieces);

        DrawDiscardQuestion();
    }

    private void DrawSlot(CustomSetDraft draft, EquipSlot slot)
    {
        var colors = theme.Colors;
        var size = new Vector2(ImGui.GetFrameHeight());
        var piece = draft.PieceIn(slot);
        var item = piece is { } p ? state.ItemOf(p.ItemId) : null;

        GameIcon.Draw(SlotIcons.IconFor(slot == EquipSlot.RingLeft ? EquipSlot.RingRight : slot), size, FontAwesomeIcon.Tshirt, colors.TextMuted);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(SlotNames.Full(slot));
        ImGui.SameLine();
        if (item != null)
            GameIcon.Draw(item.IconId, size, FontAwesomeIcon.Tshirt, colors.TextMuted);
        else
            ImGui.Dummy(size);
        ImGui.SameLine();

        // The name fills the row up to the dye fields and the remove button.
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = MathF.Max(40, ImGui.GetContentRegionAvail().X - size.X * 3 - spacing * 3);
        var label = piece == null ? "leer" : item?.Name ?? "Unbekanntes Item";
        using (ImRaii.PushColor(ImGuiCol.Text, colors.TextMuted, piece == null))
        {
            if (ImGui.Selectable($"{label}##slot", editor.SelectedSlot == slot, ImGuiSelectableFlags.None, new Vector2(width, size.Y)))
                editor.SelectSlot(slot);
        }

        var dyeCount = item?.DyeCount ?? 0;
        var stain0 = piece?.Stain0 ?? 0;
        ImGui.SameLine();
        if (DyePicker.Draw("##dye1", ref stain0, dyes, theme, dyeCount >= 1))
            editor.SetStain(slot, 1, stain0, dyeCount);
        var stain1 = piece?.Stain1 ?? 0;
        ImGui.SameLine();
        if (DyePicker.Draw("##dye2", ref stain1, dyes, theme, dyeCount >= 2))
            editor.SetStain(slot, 2, stain1, dyeCount);
        ImGui.SameLine();
        using (ImRaii.Disabled(piece == null))
        {
            if (IconButton.Draw("##remove", 0, FontAwesomeIcon.Times, "Entfernen", theme))
                editor.Remove(slot);
        }
    }

    /// <summary>"Änderungen verwerfen?" while a new start waits for the unsaved draft; a click outside keeps the draft.</summary>
    private void DrawDiscardQuestion()
    {
        const string popup = "##discard";
        if (!editor.AsksToDiscard)
        {
            discardAsked = false;
            return;
        }
        if (!ImGui.IsPopupOpen(popup))
        {
            if (discardAsked)
            {
                editor.Answer(false);
                discardAsked = false;
                return;
            }
            ImGui.OpenPopup(popup);
            discardAsked = true;
        }
        if (!ImGui.BeginPopup(popup))
            return;
        ImGui.TextUnformatted("Änderungen verwerfen?");
        if (ImGui.Button("Verwerfen"))
        {
            editor.Answer(true);
            discardAsked = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Abbrechen"))
        {
            editor.Answer(false);
            discardAsked = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
