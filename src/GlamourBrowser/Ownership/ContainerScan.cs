using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace GlamourBrowser.Ownership;

internal static unsafe class ContainerScan
{
    /// <summary>Item ids in the given containers, or null when any of them is not loaded yet.</summary>
    public static List<uint>? ReadItemIds(params InventoryType[] types)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            return null;

        var result = new List<uint>();
        foreach (var type in types)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded)
                return null;

            for (var i = 0; i < container->Size; i++)
            {
                var itemId = container->Items[i].ItemId;
                if (itemId != 0)
                    result.Add(itemId);
            }
        }
        return result;
    }
}
