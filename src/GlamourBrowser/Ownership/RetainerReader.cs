using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Ownership;

/// <summary>Reads a retainer's inventory while its inventory window is open.</summary>
public sealed unsafe class RetainerReader : IDisposable
{
    private static readonly string[] Addons = ["InventoryRetainer", "InventoryRetainerLarge"];

    private static readonly InventoryType[] Pages =
    [
        InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4,
        InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7,
    ];

    private readonly OwnershipService service;

    public RetainerReader(OwnershipService service)
    {
        this.service = service;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, Addons, OnAddon);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, Addons, OnAddon);
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        var retainer = RetainerManager.Instance()->GetActiveRetainer();
        if (retainer == null || retainer->RetainerId == 0)
            return;

        var ids = ContainerScan.ReadItemIds(Pages);
        if (ids == null)
            return;

        var id = new ContainerId(ContainerKind.Retainer, retainer->RetainerId.ToString());
        service.Submit(new ContainerSnapshot(id, $"Gehilfe {retainer->NameString}", DateTimeOffset.Now, ids));
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, Addons, OnAddon);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, Addons, OnAddon);
    }
}
