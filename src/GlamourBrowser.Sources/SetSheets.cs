using GlamourBrowser.Core.Sets;
using Lumina;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

/// <summary>Reads the inputs of <see cref="SetDetection"/>: equippable items, dresser outfits and mog station sets.</summary>
public static class SetSheets
{
    private static readonly IReadOnlySet<uint> NoJobs = new HashSet<uint>();

    public static (IReadOnlyList<SetCandidate> Items, IReadOnlyList<SheetSet> Dresser, IReadOnlyList<SheetSet> MogStation) Read(GameData data)
    {
        var jobs = ClassJobCategories.JobsByCategory(data);
        var items = data.GetExcelSheet<Item>()!;
        var candidates = new List<SetCandidate>();
        foreach (var item in items)
        {
            if (item.ModelMain == 0 || item.Name.IsEmpty || item.EquipSlotCategory.RowId == 0 || ItemSlots.SlotOf(item.EquipSlotCategory.Value) is not { } slot)
                continue;
            candidates.Add(new SetCandidate(item.RowId, item.Name.ExtractText(), slot, (ushort)item.ModelMain, (byte)(item.ModelMain >> 16),
                item.LevelItem.RowId, item.ClassJobCategory.RowId, jobs.GetValueOrDefault(item.ClassJobCategory.RowId) ?? NoJobs, item.LevelEquip));
        }

        // The row id of a dresser outfit is the item id of the outfit item itself.
        var dresser = new List<SheetSet>();
        foreach (var row in data.GetExcelSheet<MirageStoreSetItem>()!)
        {
            var ids = new[] { row.MainHand, row.OffHand, row.Head, row.Body, row.Hands, row.Legs, row.Feet, row.Earrings, row.Necklace, row.Bracelets, row.Ring }
                .Select(piece => piece.RowId).Where(id => id != 0).ToList();
            if (row.RowId == 0 || ids.Count == 0)
                continue;
            var name = items.TryGetRow(row.RowId, out var outfit) ? outfit.Name.ExtractText() : "";
            dresser.Add(new SheetSet(new SetId(SetKind.Dresser, row.RowId), name, ids));
        }

        var mogStation = new List<SheetSet>();
        foreach (var row in data.GetExcelSheet<FittingShopItemSet>()!)
        {
            var ids = row.Item.Select(piece => piece.RowId).Where(id => id != 0).ToList();
            if (ids.Count > 0)
                mogStation.Add(new SheetSet(new SetId(SetKind.MogStation, row.RowId), row.Name.ExtractText(), ids));
        }
        return (candidates, dresser, mogStation);
    }
}
