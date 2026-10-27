using System;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Ownership;

/// <summary>Reads the armoire once the game has loaded it (withdraw window opened).</summary>
public sealed unsafe class ArmoireReader : IDisposable
{
    private const string Addon = "CabinetWithdraw";

    private readonly OwnershipService service;

    public ArmoireReader(OwnershipService service)
    {
        this.service = service;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, Addon, OnAddon);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, Addon, OnAddon);
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        var cabinet = &UIState.Instance()->Cabinet;
        if (!cabinet->IsCabinetLoaded())
            return;

        var items = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>()
            .Where(row => row.Item.RowId != 0 && cabinet->IsItemInCabinet(row.RowId))
            .Select(row => row.Item.RowId)
            .ToList();
        service.Submit(new ContainerSnapshot(new ContainerId(ContainerKind.Armoire, ""), "", DateTimeOffset.Now, items));
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, Addon, OnAddon);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, Addon, OnAddon);
    }
}
