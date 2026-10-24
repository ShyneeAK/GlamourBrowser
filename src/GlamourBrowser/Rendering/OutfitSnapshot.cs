using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Rendering;

public sealed record OutfitEntry(EquipSlot Slot, uint ItemId, byte Stain0, byte Stain1);

/// <summary>What the character currently wears (glamour preferred), per slot.</summary>
public sealed record OutfitSnapshot(IReadOnlyList<OutfitEntry> Entries)
{
    public static OutfitSnapshot Empty { get; } = new([]);

    public OutfitEntry? Find(EquipSlot slot) => Entries.FirstOrDefault(e => e.Slot == slot);

    public static unsafe OutfitSnapshot FromEquipped()
    {
        var manager = InventoryManager.Instance();
        var container = manager != null ? manager->GetInventoryContainer(InventoryType.EquippedItems) : null;
        if (container == null || !container->IsLoaded)
            return Empty;

        var entries = new List<OutfitEntry>();
        foreach (var slot in System.Enum.GetValues<EquipSlot>())
        {
            // Equipped container index == CharaView slot id (waist 5 and soul crystal 13 are not in EquipSlot).
            var item = container->GetInventorySlot((int)slot);
            if (item == null || item->ItemId == 0)
                continue;
            var itemId = item->GlamourId != 0 ? item->GlamourId : item->ItemId;
            entries.Add(new OutfitEntry(slot, itemId, item->Stains[0], item->Stains[1]));
        }
        return new OutfitSnapshot(entries);
    }
}
