using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.CustomSets;
using GlamourBrowser.Data;
using GlamourBrowser.GlamourPlate;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.UI;

/// <summary>
/// The set editor in the left column: name and save / discard / transfer on top, below them one row per slot (slot, item,
/// two dyes, remove). A click on a row filters the grid to that slot; a click on a tile then puts the item in.
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
        // The limit is in UTF-8 bytes: room for umlauts, the name itself is cut to its length (also when saved).
        if (ImGui.InputTextWithHint("##setname", CustomSetStore.DefaultName, ref name, CustomSetStore.MaxNameLength * 4 + 1))
            draft.Name = name.Length > CustomSetStore.MaxNameLength ? name[..CustomSetStore.MaxNameLength] : name;

        // The actions stay above the slot list, which scrolls on its own when the pane is short.
        using (ImRaii.Disabled(!editor.CanSave))
        {
            if (ImGui.Button(Loc.T("Speichern")))
                editor.Save();
        }
        ImGui.SameLine();
        // Unsaved changes ask first: there is no undo for the draft.
        if (ImGui.Button(Loc.T("Verwerfen")))
        {
            if (draft.IsDirty)
                ImGui.OpenPopup(DiscardDraftPopup);
            else
                editor.Discard();
        }
        DrawDiscardDraftQuestion();
        ImGui.SameLine();
        PlateTransferUi.DrawButton(plates, draft.Pieces);
        DyeCounter.Draw(draft.Pieces.Select(p => (p.Stain0, p.Stain1, state.ItemOf(p.ItemId)?.DyeCount ?? 0)), dyes, theme);
        ImGui.Separator();

        using (var slots = ImRaii.Child("##slots", Vector2.Zero))
        {
            if (slots.Success)
            {
                foreach (var slot in CustomSetSubjects.Slots)
                {
                    using var id = ImRaii.PushId((int)slot);
                    DrawSlot(draft, slot);
                }
            }
        }

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
        var label = piece == null ? Loc.T("leer") : item?.Name ?? Loc.T("Unbekanntes Item");
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
            if (IconButton.Draw("##remove", 0, FontAwesomeIcon.Times, Loc.T("Entfernen"), theme))
                editor.Remove(slot);
        }
    }

    private const string DiscardDraftPopup = "##discard-draft";

    /// <summary>"Änderungen verwerfen?" after "Verwerfen" with unsaved changes; a click outside keeps the draft.</summary>
    private void DrawDiscardDraftQuestion()
    {
        if (!ImGui.BeginPopup(DiscardDraftPopup))
            return;
        ImGui.TextUnformatted(Loc.T("Änderungen verwerfen?"));
        if (ImGui.Button(Loc.T("Verwerfen")))
        {
            editor.Discard();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Abbrechen")))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
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
        ImGui.TextUnformatted(Loc.T("Änderungen verwerfen?"));
        if (ImGui.Button(Loc.T("Verwerfen")))
        {
            editor.Answer(true);
            discardAsked = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Abbrechen")))
        {
            editor.Answer(false);
            discardAsked = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
