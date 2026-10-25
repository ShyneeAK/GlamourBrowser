using System;
using System.Collections.Generic;
using System.Linq;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.CustomSets;

/// <summary>
/// The custom set being edited (one at a time): start it, put items in, save or discard. A start while the draft has
/// unsaved changes waits for "Änderungen verwerfen?". Closes at logout and when its set is deleted.
/// </summary>
public sealed class CustomSetEditor
{
    private readonly BrowserState state;
    private readonly CustomSetService customSets;
    private readonly LivePreview live;
    private readonly IReadOnlyDictionary<uint, uint> variants;
    private readonly AppearanceTracker appearance;
    private Func<CustomSetDraft>? pendingStart;
    // The store the draft belongs to: another one (a login without logout) means another character.
    private CustomSetStore? draftStore;

    /// <param name="variants">"+N" variants mapped to their base item: start pieces are shown as their base.</param>
    public CustomSetEditor(BrowserState state, CustomSetService customSets, LivePreview live, IReadOnlyDictionary<uint, uint> variants,
                           AppearanceTracker appearance)
    {
        this.appearance = appearance;
        this.state = state;
        this.customSets = customSets;
        this.live = live;
        this.variants = variants;
        customSets.Changed += OnSetsChanged;
    }

    public CustomSetDraft? Draft { get; private set; }

    public bool IsOpen => Draft != null;

    /// <summary>The slot clicked in the editor: the grid shows it, a ring goes there.</summary>
    public EquipSlot? SelectedSlot { get; private set; }

    /// <summary>A start waits for the answer to "Änderungen verwerfen?" (see <see cref="Answer"/>).</summary>
    public bool AsksToDiscard => pendingStart != null;

    public void StartNew(string name, IReadOnlyList<CustomPiece> pieces)
        => Start(() => new CustomSetDraft(null, name, pieces.Select(p => p with { ItemId = variants.GetValueOrDefault(p.ItemId, p.ItemId) }).ToList()));

    public void StartEdit(CustomSet set) => Start(() => CustomSetDraft.From(set));

    /// <summary>A new set from what the character wears (glamour preferred), with its dyes.</summary>
    public void StartFromOutfit()
        => StartNew("Aktuelles Outfit", appearance.Outfit.Entries.Select(e => new CustomPiece(e.Slot, e.ItemId, e.Stain0, e.Stain1)).ToList());

    /// <summary>The answer to "Änderungen verwerfen?": yes starts the waiting draft, no keeps the current one.</summary>
    public void Answer(bool discard)
    {
        var start = pendingStart;
        pendingStart = null;
        if (discard && start != null)
            Open(start());
    }

    /// <summary>A click on a tile: an item goes into its slot, a set brings all its pieces with their dyes.</summary>
    public void Put(TileSubject subject)
    {
        if (Draft == null)
            return;
        switch (subject)
        {
            case ItemSubject { Item: var item }:
                Draft.Put(item, SelectedSlot);
                break;
            case SetSubject set:
                Draft.PutAll(set);
                break;
        }
        UpdatePreview();
    }

    /// <summary>A click on a slot of the editor: the grid shows that slot's items (rings are all right rings in the catalog).</summary>
    public void SelectSlot(EquipSlot slot)
    {
        SelectedSlot = slot;
        var shown = slot == EquipSlot.RingLeft ? EquipSlot.RingRight : slot;
        state.Filter = state.Filter with { Mode = BrowseMode.Items, Slots = new HashSet<EquipSlot> { shown } };
    }

    public void Remove(EquipSlot slot)
    {
        Draft?.Remove(slot);
        UpdatePreview();
    }

    public void SetStain(EquipSlot slot, int channel, byte stain, int dyeCount)
    {
        if (Draft?.SetStain(slot, channel, stain, dyeCount) == true)
            UpdatePreview();
    }

    /// <summary>Stores the draft (a new set or an update of its set), shows it selected and goes back to "Meine Sets".</summary>
    public void Save()
    {
        if (Draft == null || customSets.Current is not { } store || Draft.Pieces.Count == 0)
            return;

        var now = DateTimeOffset.Now;
        var saved = Draft.Id is { } id && store.Find(id) is { } old
            ? store.Update(old with { Name = Draft.Name, Pieces = Draft.Pieces, Changed = now }) ? store.Find(id)! : old
            : store.Add(Draft.Name, Draft.Pieces, now);
        Close(CustomSetSubjects.ToSubject(saved, state.ItemOf));
    }

    public void Discard()
    {
        var kept = Draft?.Id is { } id && customSets.Current?.Find(id) is { } set ? CustomSetSubjects.ToSubject(set, state.ItemOf) : null;
        Close(kept);
    }

    private void Start(Func<CustomSetDraft> make)
    {
        if (Draft?.IsDirty == true)
        {
            pendingStart = make;
            return;
        }
        Open(make());
    }

    private void Open(CustomSetDraft draft)
    {
        Draft = draft;
        draftStore = customSets.Current;
        SelectedSlot = null;
        UpdatePreview();
    }

    /// <summary>Ends editing; the set (or nothing) stays selected, the grid shows "Meine Sets".</summary>
    private void Close(SetSubject? selected)
    {
        Draft = null;
        SelectedSlot = null;
        pendingStart = null;
        state.Selected = selected;
        live.Select(selected);
        state.Filter = state.Filter with { Mode = BrowseMode.MySets, Slots = new HashSet<EquipSlot>() };
    }

    /// <summary>The live preview and the selection show the draft as it is now.</summary>
    private void UpdatePreview()
    {
        var subject = Draft?.ToSubject(state.ItemOf);
        state.Selected = subject;
        live.Select(subject);
    }

    private void OnSetsChanged()
    {
        if (Draft == null)
        {
            // A selected custom set may be another character's now, or changed: it stays selected only while it is the same.
            var kept = CustomSetSubjects.Reselect(state.Selected, state.SetSubjectFor);
            if (!ReferenceEquals(kept, state.Selected))
            {
                state.Selected = kept;
                live.Select(kept);
            }
            return;
        }
        // Logged out, another character, or the edited set was deleted (only from its card): the draft has nothing to go back to.
        if (customSets.Current == null || customSets.Current != draftStore || Draft.Id is { } id && customSets.Current.Find(id) == null)
        {
            Draft = null;
            SelectedSlot = null;
            pendingStart = null;
            state.Selected = null;
            live.Select(null);
        }
    }
}
