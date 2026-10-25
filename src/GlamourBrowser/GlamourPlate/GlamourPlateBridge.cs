using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using CabinetRow = Lumina.Excel.Sheets.Cabinet;
using MirageStoreSetItem = Lumina.Excel.Sheets.MirageStoreSetItem;
using ItemSource = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMiragePrismMiragePlateData.ItemSource;

namespace GlamourBrowser.GlamourPlate;

/// <param name="EditorClosed">The plate editor was not (or no longer) open; nothing or only part was transferred.</param>
/// <param name="Notes">Only what went wrong or is missing: the piece (or slot) and why.</param>
public sealed record TransferResult(bool EditorClosed, int Transferred, int Total, IReadOnlyList<(string Name, string Reason)> Notes, DateTime Finished)
{
    public bool HasProblems => EditorClosed || Notes.Count > 0;
}

/// <summary>
/// Talks to the game's glamour plate editor: reads the saved plates and fills the plate being edited with a set.
/// Filling runs on the framework thread over a few frames: every slot is set, checked, and set again when the game
/// did not take it (it ignores some calls, see the spike in the C spec).
/// </summary>
public sealed unsafe class GlamourPlateBridge : IDisposable
{
    private const int MaxTries = 3;

    private readonly IReadOnlyDictionary<uint, uint> variants;
    private readonly OwnershipService ownership;
    private readonly DyeCatalog dyes;
    private readonly Func<uint, string> nameOf;
    private readonly Func<uint, int> dyeCountOf;
    private readonly Dictionary<uint, IReadOnlyList<uint>> outfitSets;
    private Transfer? running;

    private sealed class Transfer(IReadOnlyList<(int Index, PlateSlot Slot)> steps, uint selectedBefore)
    {
        public List<(int Index, PlateSlot Slot)> Pending { get; } = steps.ToList();
        public uint SelectedBefore { get; } = selectedBefore;
        public int Round { get; set; }
        public List<(int Index, PlateSlot Slot)> Done { get; } = new();
        public List<PlateSlot> Failed { get; } = new();
        // Slots whose piece is in but whose dyes the editor did not mark (yet).
        public HashSet<int> DyeFailed { get; } = new();
        public IReadOnlyList<PlateSlot> Plan { get; init; } = [];
    }

    /// <param name="nameOf">The item's name for the result list.</param>
    /// <param name="dyeCountOf">The item's dye channels (0–2).</param>
    public GlamourPlateBridge(IReadOnlyDictionary<uint, uint> variants, OwnershipService ownership, DyeCatalog dyes, Func<uint, string> nameOf,
                              Func<uint, int> dyeCountOf)
    {
        this.dyeCountOf = dyeCountOf;
        this.variants = variants;
        this.ownership = ownership;
        this.dyes = dyes;
        this.nameOf = nameOf;
        outfitSets = Plugin.DataManager.GetExcelSheet<MirageStoreSetItem>()
            .ToDictionary(row => row.RowId, row => (IReadOnlyList<uint>)new[]
                {
                    row.MainHand.RowId, row.OffHand.RowId, row.Head.RowId, row.Body.RowId, row.Hands.RowId, row.Legs.RowId,
                    row.Feet.RowId, row.Earrings.RowId, row.Necklace.RowId, row.Bracelets.RowId, row.Ring.RowId,
                }.Where(id => id != 0).ToList());
        Plugin.Framework.Update += OnUpdate;
    }

    /// <summary>The game's plate editor is open (at the dresser), so a set can be transferred.</summary>
    /// <remarks>
    /// <c>Data->OpenMode</c> is not checked: it was 0 in every dump while editing and what its other values mean is not
    /// known. A mode in which the editor does not take pieces would show as "vom Spiel nicht übernommen" after the
    /// transfer, since every slot is checked.
    /// </remarks>
    public bool IsEditorOpen
    {
        get
        {
            var agent = AgentMiragePrismMiragePlate.Instance();
            return agent != null && agent->IsAgentActive() && agent->Data != null;
        }
    }

    public bool IsTransferring => running != null;

    /// <summary>The outcome of the last transfer until the UI has shown it (<see cref="ClearResult"/>).</summary>
    public TransferResult? LastResult { get; private set; }

    public void ClearResult() => LastResult = null;

    /// <summary>The game has the saved plates in memory (after the plates were opened at the dresser).</summary>
    public bool PlatesLoaded
    {
        get
        {
            var mirage = MirageManager.Instance();
            return mirage != null && mirage->GlamourPlatesLoaded;
        }
    }

    public int PlateCount
    {
        get
        {
            var mirage = MirageManager.Instance();
            return mirage != null ? mirage->GlamourPlates.Length : 0;
        }
    }

    /// <summary>The pieces of a saved plate the catalog knows, null while the plates are not loaded.</summary>
    /// <param name="isKnown">Whether the catalog knows the item; a "+N" variant counts as its base.</param>
    public IReadOnlyList<CustomPiece>? ReadPlate(int index, Func<uint, bool> isKnown)
    {
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->GlamourPlatesLoaded || index < 0 || index >= mirage->GlamourPlates.Length)
            return null;
        ref var plate = ref mirage->GlamourPlates[index];
        return PlateTransfer.Read(plate.ItemIds, plate.Stain0Ids, plate.Stain1Ids, id => isKnown(variants.GetValueOrDefault(id, id)));
    }

    /// <summary>Starts filling the plate being edited with the pieces; the outcome lands in <see cref="LastResult"/>.</summary>
    public void Start(IReadOnlyList<CustomPiece> pieces)
    {
        lock (RenderSync.Lock)
        {
            var agent = AgentMiragePrismMiragePlate.Instance();
            if (running != null)
                return;
            if (agent == null || !agent->IsAgentActive() || agent->Data == null)
            {
                LastResult = new TransferResult(true, 0, pieces.Count, [], DateTime.Now);
                return;
            }

            var plan = PlateTransfer.Plan(pieces, DresserLookup(), ArmoireLookup(), PlacesOf);
            running = new Transfer(plan.Select((slot, index) => (index, slot)).ToList(), agent->Data->SelectedItemIndex) { Plan = plan };
        }
    }

    private void OnUpdate(IFramework framework)
    {
        lock (RenderSync.Lock)
        {
            if (running is not { } transfer)
                return;

            var agent = AgentMiragePrismMiragePlate.Instance();
            if (agent == null || !agent->IsAgentActive() || agent->Data == null)
            {
                Finish(transfer, editorClosed: true);
                return;
            }

            // Each round sets and checks every slot still pending; those the game did not take wait for the next frame.
            foreach (var (index, slot) in transfer.Pending.ToList())
            {
                var (itemTaken, dyesTaken) = Apply(agent, index, slot);
                if (dyesTaken)
                    transfer.DyeFailed.Remove(index);
                else
                    transfer.DyeFailed.Add(index);
                if (itemTaken && dyesTaken)
                {
                    transfer.Pending.Remove((index, slot));
                    transfer.Done.Add((index, slot));
                }
            }
            // A later call may change a slot set before it: a done slot that no longer shows its piece goes again.
            foreach (var step in transfer.Done.Where(s => Normalize(agent->Data->CurrentItems[s.Index].ItemId) != Expected(s.Slot)).ToList())
            {
                transfer.Done.Remove(step);
                transfer.Pending.Add(step);
            }
            if (transfer.Pending.Count > 0 && ++transfer.Round < MaxTries)
                return;

            // The piece is in but its dyes were not marked: done, with a note; otherwise the slot failed.
            foreach (var (index, slot) in transfer.Pending)
            {
                if (agent->Data->CurrentItems[index].ItemId is var shown && Normalize(shown) == Expected(slot))
                    transfer.Done.Add((index, slot));
                else
                    transfer.Failed.Add(slot);
            }
            agent->Data->SelectedItemIndex = transfer.SelectedBefore;
            Finish(transfer, editorClosed: false);
        }
    }

    /// <summary>
    /// Sets (or clears) one plate slot, marks the set's dyes where they differ from the stored ones (paid when the
    /// player saves the plate), and tells whether the editor shows the piece and the dyes afterwards.
    /// </summary>
    private (bool Item, bool Dyes) Apply(AgentMiragePrismMiragePlate* agent, int index, PlateSlot slot)
    {
        agent->Data->SelectedItemIndex = (uint)index;
        if (slot.Source is not { } source)
        {
            agent->ClearItemData((uint)index);
            return (Normalize(agent->Data->CurrentItems[index].ItemId) == 0, true);
        }

        // The dresser holds values that are no dyes for some pieces (e.g. 232 on a necklace): only real dyes of existing channels go along.
        var piece = slot.Piece!.Value;
        var dyeCount = dyeCountOf(piece.ItemId);
        var (base0, base1) = PlateDyes.BaseStains(source, dyeCount, IsDye);
        agent->SetSelectedItemData(source.Kind == PlateSourceKind.Dresser ? ItemSource.PrismBox : ItemSource.Cabinet, source.Id, source.ItemId, base0, base1);
        if (Normalize(agent->Data->CurrentItems[index].ItemId) != Expected(slot))
            return (false, false);

        var (first, second) = PlateDyes.Changes(piece, source, dyeCount, IsDye, dyes.ItemsOf);
        if (first == null && second == null)
            return (true, true);

        // SetSelectedItemStains dyes the item of the editor's context menu, not SelectedItemIndex (dye spike: it dyed the
        // body while slot 4 was selected); both channels go in one call, an unchanged one keeps its stain without an item.
        // The channel flags are set first, as Glamaholic does: a freshly set item may not have them yet.
        var item0 = DyeItem(first, out var id0);
        var item1 = DyeItem(second, out var id1);
        ref var shown = ref agent->Data->CurrentItems[index];
        if (dyeCount >= 1)
            shown.Flags |= AgentMiragePrismMiragePlateData.ItemFlag.HasStain0;
        if (dyeCount >= 2)
            shown.Flags |= AgentMiragePrismMiragePlateData.ItemFlag.HasStain1;
        var contextBefore = agent->Data->ContextMenuItemIndex;
        agent->Data->ContextMenuItemIndex = (uint)index;
        agent->SetSelectedItemStains(item0, first?.Stain ?? base0, id0, item1, second?.Stain ?? base1, id1);
        agent->Data->ContextMenuItemIndex = contextBefore;
        var marked = (first == null || shown.PendingStainIds[0] == first.Value.Stain && shown.PendingStainItemIds[0] != 0)
                     && (second == null || shown.PendingStainIds[1] == second.Value.Stain && shown.PendingStainItemIds[1] != 0);
        return (true, marked);
    }

    /// <summary>
    /// The dye item to take: a stack in the inventory like the editor does by hand. Without one only its id goes along;
    /// the editor then marks the dye "from item id" and the player gets the dye before saving, as when dyeing by hand.
    /// Null and id 0 leave the channel as it is.
    /// </summary>
    private static InventoryItem* DyeItem(PlateDye? dye, out uint itemId)
    {
        itemId = 0;
        if (dye is not { ItemIds.Count: > 0 } wanted)
            return null;
        var manager = InventoryManager.Instance();
        if (manager != null)
        {
            foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
            {
                var container = manager->GetInventoryContainer(type);
                if (container == null || !container->IsLoaded)
                    continue;
                for (var i = 0; i < container->Size; i++)
                {
                    var item = container->GetInventorySlot(i);
                    if (item != null && item->ItemId != 0 && wanted.ItemIds.Contains(item->ItemId))
                    {
                        itemId = item->ItemId;
                        return item;
                    }
                }
            }
        }
        itemId = wanted.ItemIds[0];
        return null;
    }

    /// <summary>"No dye" or a dye of the Stain sheet.</summary>
    private bool IsDye(byte stain) => stain == 0 || dyes.Find(stain) != null;

    private static uint Expected(PlateSlot slot) => slot.Source is { } source ? Normalize(source.ItemId) : 0;

    private void Finish(Transfer transfer, bool editorClosed)
    {
        running = null;
        var dyeFailed = transfer.DyeFailed.Select(i => transfer.Plan[i].Slot).ToList();
        var report = PlateTransfer.Report(transfer.Plan, transfer.Done.Select(s => s.Slot.Slot).ToList(), transfer.Failed.Select(s => s.Slot).ToList(),
            dyeFailed, editorClosed, nameOf, UI.SlotNames.Full);
        LastResult = new TransferResult(editorClosed, report.Transferred, report.Total, report.Notes, DateTime.Now);
    }

    private Func<uint, PlateSource?> DresserLookup()
    {
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->PrismBoxLoaded)
            return _ => null;
        return PlateSources.Dresser(mirage->PrismBoxItemIds.ToArray(), mirage->PrismBoxStain0Ids.ToArray(), mirage->PrismBoxStain1Ids.ToArray(),
            outfitSets, variants);
    }

    private Func<uint, PlateSource?> ArmoireLookup()
    {
        var cabinet = &UIState.Instance()->Cabinet;
        if (!cabinet->IsCabinetLoaded())
            return _ => null;
        var rows = Plugin.DataManager.GetExcelSheet<CabinetRow>()
            .Where(row => row.Item.RowId != 0 && cabinet->IsItemInCabinet(row.RowId))
            .Select(row => (Row: row.RowId, ItemId: row.Item.RowId))
            .ToList();
        return PlateSources.Armoire(rows, variants);
    }

    /// <summary>Every saved plate that is not empty and not already a custom set becomes one ("Glamour-Plate {n}").</summary>
    /// <returns>How many were imported, and how many non-empty plates were skipped as already there.</returns>
    /// <param name="isKnown">Whether the catalog knows the item, see <see cref="ReadPlate"/>.</param>
    public (int Imported, int Skipped) ImportAll(CustomSetStore store, Func<uint, bool> isKnown)
    {
        var plates = Enumerable.Range(0, PlateCount).Select(i => ReadPlate(i, isKnown) ?? []).ToList();
        var imported = PlateImport.NewSets(plates, store.Sets, variants);
        var now = DateTimeOffset.Now;
        // Oldest plate first, so that plate 1 ends up last changed and on top.
        foreach (var (plate, pieces) in imported.Reverse())
            store.Add($"Glamour-Plate {plate + 1}", pieces, now);
        return (imported.Count, plates.Count(p => p.Count > 0) - imported.Count);
    }

    private IReadOnlyList<string> PlacesOf(uint itemId) => ownership.Current?.LocationsOf(itemId).Select(l => l.DisplayName).ToList() ?? [];

    private static uint Normalize(uint raw) => raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw;

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;
}
