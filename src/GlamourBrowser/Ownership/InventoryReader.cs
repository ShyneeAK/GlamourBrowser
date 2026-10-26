using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Core.Ownership;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Ownership;

/// <summary>Reads the player's own containers on login and after inventory changes.</summary>
public sealed class InventoryReader : IDisposable
{
    private static readonly (ContainerKind Kind, string Name, InventoryType[] Types)[] Containers =
    [
        (ContainerKind.Inventory, "Inventar", [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4]),
        (ContainerKind.Equipped, "Ausgerüstet", [InventoryType.EquippedItems]),
        (ContainerKind.Armoury, "Arsenal", [
            InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
            InventoryType.ArmoryHands, InventoryType.ArmoryWaist, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
            InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings]),
        (ContainerKind.Saddlebag, "Satteltasche", [InventoryType.SaddleBag1, InventoryType.SaddleBag2]),
        (ContainerKind.PremiumSaddlebag, "Satteltasche (Premium)", [InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2]),
    ];

    private readonly OwnershipService service;
    private bool dirty = true;

    public InventoryReader(OwnershipService service)
    {
        this.service = service;
        Plugin.GameInventory.InventoryChanged += OnInventoryChanged;
        Plugin.ClientState.Login += MarkDirty;
        Plugin.Framework.Update += OnUpdate;
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events) => dirty = true;

    private void MarkDirty() => dirty = true;

    // At most one read per framework tick, however many change events arrived.
    private void OnUpdate(IFramework framework)
    {
        if (!dirty || service.Current == null)
            return;
        dirty = false;

        var now = DateTimeOffset.Now;
        foreach (var (kind, name, types) in Containers)
        {
            var ids = ContainerScan.ReadItemIds(types);
            if (ids != null)
                service.Submit(new ContainerSnapshot(new ContainerId(kind, ""), Loc.T(name), now, ids));
        }
    }

    public void Dispose()
    {
        Plugin.GameInventory.InventoryChanged -= OnInventoryChanged;
        Plugin.ClientState.Login -= MarkDirty;
        Plugin.Framework.Update -= OnUpdate;
    }
}
