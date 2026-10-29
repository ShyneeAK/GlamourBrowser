using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Core.Ownership;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Ownership;

/// <summary>Reads the glamour dresser once the game has loaded it (window opened), with the dyes of its entries.</summary>
public sealed unsafe class DresserReader : IDisposable
{
    private const string Addon = "MiragePrismPrismBox";

    private readonly OwnershipService service;
    private readonly Dictionary<uint, IReadOnlyList<uint>> outfitSets;

    public DresserReader(OwnershipService service)
    {
        this.service = service;
        outfitSets = Plugin.DataManager.GetExcelSheet<MirageStoreSetItem>()
            .ToDictionary(row => row.RowId, row => (IReadOnlyList<uint>)new[]
                {
                    row.MainHand.RowId, row.OffHand.RowId, row.Head.RowId, row.Body.RowId, row.Hands.RowId, row.Legs.RowId,
                    row.Feet.RowId, row.Earrings.RowId, row.Necklace.RowId, row.Bracelets.RowId, row.Ring.RowId,
                }.Where(id => id != 0).ToList());
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, Addon, OnAddon);
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->PrismBoxLoaded)
            return;

        var rawIds = mirage->PrismBoxItemIds.ToArray();
        var (stain0, stain1) = (mirage->PrismBoxStain0Ids.ToArray(), mirage->PrismBoxStain1Ids.ToArray());
        var items = DresserContents.Resolve(rawIds, outfitSets);
        // The empty places stay out: only the dyes of the entries matter (their index is read anew for a transfer).
        var entries = rawIds.Select((raw, i) => new DresserEntry(raw, stain0[i], stain1[i])).Where(e => e.RawId != 0).ToList();
        service.Submit(new ContainerSnapshot(new ContainerId(ContainerKind.Dresser, ""), "", DateTimeOffset.Now, items) { Entries = entries });
    }

    public void Dispose() => Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, Addon, OnAddon);
}
