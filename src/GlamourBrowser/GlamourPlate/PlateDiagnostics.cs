using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Core.Ownership;
using CabinetRow = Lumina.Excel.Sheets.Cabinet;
using ItemRow = Lumina.Excel.Sheets.Item;
using ItemSource = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMiragePrismMiragePlateData.ItemSource;

namespace GlamourBrowser.GlamourPlate;

/// <summary>
/// /gbrowser plate …: logs the glamour plate editor's state and sets single slots, to learn how the game's plate
/// agent behaves (slot order, sources, dyes). Call on the framework thread.
/// </summary>
public static unsafe class PlateDiagnostics
{
    public static void Run(string[] args)
    {
        switch (args.FirstOrDefault())
        {
            case null:
                Dump();
                break;
            case "box":
                ListDresser(args.Length > 1 ? string.Join(' ', args.Skip(1)) : "");
                break;
            case "armoire":
                ListArmoire(args.Length > 1 ? string.Join(' ', args.Skip(1)) : "");
                break;
            case "set" when args.Length >= 3 && int.TryParse(args[1], out var slot) && uint.TryParse(args[2], out var index):
                Set(slot, index, args.Length >= 5 && byte.TryParse(args[3], out var s0) ? s0 : null, args.Length >= 5 && byte.TryParse(args[4], out var s1) ? s1 : null,
                    args.Length >= 6 && uint.TryParse(args[5], out var other) ? other : null);
                break;
            case "cabinet" when args.Length >= 3 && int.TryParse(args[1], out var slot) && args[2] == "first":
                if (FirstBodyInArmoire() is { } first)
                    SetFromCabinet(slot, first);
                break;
            case "cabinet" when args.Length >= 3 && int.TryParse(args[1], out var slot) && uint.TryParse(args[2], out var row):
                SetFromCabinet(slot, row);
                break;
            case "clear" when args.Length >= 2 && int.TryParse(args[1], out var slot):
                Clear(slot);
                break;
            default:
                Log("usage: /gbrowser plate [box <name>|armoire <name>|set <slot> <dresserIndex> [stain0 stain1 [itemId]]|cabinet <slot> <cabinetRow>|clear <slot>]");
                break;
        }
    }

    public static void Dump()
    {
        var agent = AgentMiragePrismMiragePlate.Instance();
        if (agent == null)
        {
            Log("agent not available");
            return;
        }

        Log($"editor active {agent->IsAgentActive()}, data {(agent->Data != null ? "yes" : "no")}");
        var data = agent->Data;
        if (data != null)
        {
            Log($"selected plate {data->SelectedMiragePlateIndex}, selected item {data->SelectedItemIndex}, changes {data->HasChanges}, open mode {data->OpenMode}");
            var items = data->CurrentItems;
            for (var i = 0; i < items.Length; i++)
            {
                ref var item = ref items[i];
                Log($"  current[{i}] item {item.ItemId} {NameOf(item.ItemId)} | source {item.Source} id {item.SourceId} | stains {item.StainIds[0]}/{item.StainIds[1]} "
                    + $"| pending {item.PendingStainIds[0]}/{item.PendingStainIds[1]} | slot category {item.EquipSlotCategory} | stain state {item.StainState}");
            }
        }

        var mirage = MirageManager.Instance();
        if (mirage == null)
            return;
        Log($"plates loaded {mirage->GlamourPlatesLoaded}, dresser loaded {mirage->PrismBoxLoaded}");
        if (mirage->GlamourPlatesLoaded && data != null && data->SelectedMiragePlateIndex < mirage->GlamourPlates.Length)
        {
            ref var plate = ref mirage->GlamourPlates[(int)data->SelectedMiragePlateIndex];
            for (var i = 0; i < plate.ItemIds.Length; i++)
                Log($"  saved[{i}] item {plate.ItemIds[i]} {NameOf(plate.ItemIds[i])} | stains {plate.Stain0Ids[i]}/{plate.Stain1Ids[i]}");
        }
    }

    private static void ListDresser(string filter)
    {
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->PrismBoxLoaded)
        {
            Log("dresser not loaded (open it first)");
            return;
        }

        var shown = 0;
        for (var i = 0; i < mirage->PrismBoxItemIds.Length && shown < 30; i++)
        {
            var raw = mirage->PrismBoxItemIds[i];
            if (raw == 0)
                continue;
            var name = NameOf(Normalize(raw));
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;
            Log($"  dresser[{i}] raw {raw} item {Normalize(raw)} {name} | stains {mirage->PrismBoxStain0Ids[i]}/{mirage->PrismBoxStain1Ids[i]}");
            shown++;
        }
    }

    private static void ListArmoire(string filter)
    {
        var cabinet = &UIState.Instance()->Cabinet;
        if (!cabinet->IsCabinetLoaded())
        {
            Log("armoire not loaded (open it first)");
            return;
        }

        foreach (var row in Plugin.DataManager.GetExcelSheet<CabinetRow>().Where(r => r.Item.RowId != 0 && cabinet->IsItemInCabinet(r.RowId)))
        {
            var name = NameOf(row.Item.RowId);
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;
            Log($"  armoire row {row.RowId} item {row.Item.RowId} {name}");
        }
    }

    /// <param name="itemId">Another item than the dresser entry's, e.g. one piece of a stored outfit.</param>
    /// <summary>The armoire row of the first body piece in the armoire (logged), null when the armoire is not loaded.</summary>
    private static uint? FirstBodyInArmoire()
    {
        var cabinet = &UIState.Instance()->Cabinet;
        if (!cabinet->IsCabinetLoaded())
        {
            Log("armoire not loaded (show it in the plate editor first)");
            return null;
        }
        var rows = Plugin.DataManager.GetExcelSheet<CabinetRow>();
        foreach (var row in rows.Where(r => r.Item.RowId != 0 && cabinet->IsItemInCabinet(r.RowId)))
        {
            if (row.Item.Value.EquipSlotCategory.Value.Body == 1)
                return row.RowId;
        }
        Log("no body piece in the armoire");
        return null;
    }

    private static void Set(int slot, uint index, byte? stain0, byte? stain1, uint? itemId)
    {
        var agent = OpenData();
        if (agent == null)
            return;
        var mirage = MirageManager.Instance();
        if (mirage == null || !mirage->PrismBoxLoaded || index >= mirage->PrismBoxItemIds.Length)
        {
            Log("dresser not loaded or index out of range");
            return;
        }

        itemId ??= Normalize(mirage->PrismBoxItemIds[(int)index]);
        var s0 = stain0 ?? mirage->PrismBoxStain0Ids[(int)index];
        var s1 = stain1 ?? mirage->PrismBoxStain1Ids[(int)index];
        Log($"set slot {slot} from dresser[{index}] item {itemId} {NameOf(itemId.Value)} stains {s0}/{s1}");
        agent->Data->SelectedItemIndex = (uint)slot;
        agent->SetSelectedItemData(ItemSource.PrismBox, index, itemId.Value, s0, s1);
        Dump();
    }

    private static void SetFromCabinet(int slot, uint row)
    {
        var agent = OpenData();
        if (agent == null)
            return;
        if (!Plugin.DataManager.GetExcelSheet<CabinetRow>().TryGetRow(row, out var cabinetRow))
        {
            Log($"no cabinet row {row}");
            return;
        }

        var itemId = cabinetRow.Item.RowId;
        Log($"set slot {slot} from armoire row {row} item {itemId} {NameOf(itemId)}, in armoire {UIState.Instance()->Cabinet.IsItemInCabinet(row)}");
        agent->Data->SelectedItemIndex = (uint)slot;
        agent->SetSelectedItemData(ItemSource.Cabinet, row, itemId, 0, 0);
        Dump();
    }

    private static void Clear(int slot)
    {
        var agent = OpenData();
        if (agent == null)
            return;
        Log($"clear slot {slot}");
        agent->ClearItemData((uint)slot);
        Dump();
    }

    /// <summary>The plate agent when the editor is open with its data, else null (logged).</summary>
    private static AgentMiragePrismMiragePlate* OpenData()
    {
        var agent = AgentMiragePrismMiragePlate.Instance();
        if (agent != null && agent->IsAgentActive() && agent->Data != null)
            return agent;
        Log("glamour plate editor is not open");
        return null;
    }

    private static uint Normalize(uint raw) => raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw;

    private static string NameOf(uint itemId)
        => itemId != 0 && Plugin.DataManager.GetExcelSheet<ItemRow>().TryGetRow(itemId, out var item) ? item.Name.ExtractText() : "";

    private static void Log(string message) => Plugin.Log.Information($"[plate] {message}");
}
