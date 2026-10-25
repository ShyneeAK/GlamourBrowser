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
/// <param name="Notes">Per piece that needs attention: its name and why ("liegt in: …", "im Spiel färben: …", "vom Spiel nicht übernommen – …").</param>
public sealed record TransferResult(bool EditorClosed, int Transferred, int Total, IReadOnlyList<(string Name, string Reason)> Notes);

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
    private readonly Dictionary<uint, IReadOnlyList<uint>> outfitSets;
    private Transfer? running;

    private sealed class Transfer(IReadOnlyList<(int Index, PlateSlot Slot)> steps, uint selectedBefore)
    {
        public List<(int Index, PlateSlot Slot)> Pending { get; } = steps.ToList();
        public uint SelectedBefore { get; } = selectedBefore;
        public int Round { get; set; }
        public List<PlateSlot> Done { get; } = new();
        public List<PlateSlot> Failed { get; } = new();
        public IReadOnlyList<PlateSlot> Plan { get; init; } = [];
    }

    /// <param name="nameOf">The item's name for the result list.</param>
    public GlamourPlateBridge(IReadOnlyDictionary<uint, uint> variants, OwnershipService ownership, DyeCatalog dyes, Func<uint, string> nameOf)
    {
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

    /// <summary>The pieces of a saved plate, null while the plates are not loaded.</summary>
    public IReadOnlyList<CustomPiece>? ReadPlate(int index)
    {
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->GlamourPlatesLoaded || index < 0 || index >= mirage->GlamourPlates.Length)
            return null;
        ref var plate = ref mirage->GlamourPlates[index];
        return PlateTransfer.Read(plate.ItemIds, plate.Stain0Ids, plate.Stain1Ids);
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
                LastResult = new TransferResult(true, 0, pieces.Count, []);
                return;
            }

            var plan = PlateTransfer.Plan(pieces, DresserLookup(), ArmoireLookup(), PlacesOf, dyes.NameOf);
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
                if (Apply(agent, index, slot))
                {
                    transfer.Pending.Remove((index, slot));
                    transfer.Done.Add(slot);
                }
            }
            if (transfer.Pending.Count > 0 && ++transfer.Round < MaxTries)
                return;

            transfer.Failed.AddRange(transfer.Pending.Select(p => p.Slot));
            agent->Data->SelectedItemIndex = transfer.SelectedBefore;
            Finish(transfer, editorClosed: false);
        }
    }

    /// <summary>Sets (or clears) one plate slot and tells whether the editor shows it afterwards.</summary>
    private static bool Apply(AgentMiragePrismMiragePlate* agent, int index, PlateSlot slot)
    {
        agent->Data->SelectedItemIndex = (uint)index;
        if (slot.Source is { } source)
        {
            agent->SetSelectedItemData(source.Kind == PlateSourceKind.Dresser ? ItemSource.PrismBox : ItemSource.Cabinet, source.Id, source.ItemId,
                source.Stain0, source.Stain1);
        }
        else
        {
            agent->ClearItemData((uint)index);
        }

        var shown = agent->Data->CurrentItems[index].ItemId;
        var expected = slot.Source is { } s ? Normalize(s.ItemId) : 0;
        return Normalize(shown) == expected;
    }

    private void Finish(Transfer transfer, bool editorClosed)
    {
        running = null;
        var report = PlateTransfer.Report(transfer.Plan, transfer.Done.Select(s => s.Slot).ToList(), transfer.Failed.Select(s => s.Slot).ToList(),
            editorClosed, nameOf, UI.SlotNames.Full);
        LastResult = new TransferResult(editorClosed, report.Transferred, report.Total, report.Notes);
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

    private IReadOnlyList<string> PlacesOf(uint itemId) => ownership.Current?.LocationsOf(itemId).Select(l => l.DisplayName).ToList() ?? [];

    private static uint Normalize(uint raw) => raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw;

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;
}
