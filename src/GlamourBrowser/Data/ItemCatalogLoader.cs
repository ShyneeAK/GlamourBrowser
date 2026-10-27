using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Sources;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Data;

/// <summary>
/// Builds the item catalog from Lumina: every equippable item that has a model, except "+N" variants that look like
/// their base item (those are returned separately, mapped to the base).
/// </summary>
public static class ItemCatalogLoader
{
    public static (IReadOnlyList<GlamourItem> Items, IReadOnlyDictionary<uint, uint> Variants) Load(IDataManager data)
    {
        var jobsByCategory = ClassJobCategories.JobsByCategory(data.GameData);
        var variants = VariantSheet.BaseOf(data.GameData);
        var restrictions = new Dictionary<uint, RaceGenderRestriction>();
        var result = new List<GlamourItem>();

        foreach (var item in data.GetExcelSheet<Item>())
        {
            if (item.EquipSlotCategory.RowId == 0 || item.ModelMain == 0 || item.Name.IsEmpty || variants.ContainsKey(item.RowId))
                continue;
            if (ItemSlots.SlotOf(item.EquipSlotCategory.Value) is not { } slot)
                continue;

            jobsByCategory.TryGetValue(item.ClassJobCategory.RowId, out var jobs);
            result.Add(new GlamourItem(
                item.RowId,
                item.Name.ExtractText(),
                item.Icon,
                slot,
                jobs ?? new HashSet<uint>(),
                item.LevelEquip,
                item.DyeCount,
                RestrictionOf(item.EquipRestriction.RowId, data, restrictions),
                Tradabilities.From(item.IsUntradable, item.ItemSearchCategory.RowId),
                ItemSlots.BlocksOf(item.EquipSlotCategory.Value),
                item.ModelMain,
                item.ModelSub));
        }

        Plugin.Log.Information($"Item catalog: {result.Count} items, {variants.Count} \"+N\" variants merged into their base item");
        return (result, variants);
    }

    private static RaceGenderRestriction RestrictionOf(uint rowId, IDataManager data, Dictionary<uint, RaceGenderRestriction> cache)
    {
        if (rowId == 0)
            return RaceGenderRestriction.None;
        if (cache.TryGetValue(rowId, out var cached))
            return cached;

        var row = data.GetExcelSheet<EquipRaceCategory>().GetRow(rowId);
        var races = new HashSet<byte>();
        if (row.Hyur) races.Add(1);
        if (row.Elezen) races.Add(2);
        if (row.Lalafell) races.Add(3);
        if (row.Miqote) races.Add(4);
        if (row.Roegadyn) races.Add(5);
        if (row.AuRa) races.Add(6);
        if (row.Hrothgar) races.Add(7);
        if (row.Viera) races.Add(8);
        return cache[rowId] = new RaceGenderRestriction(races, row.Male, row.Female);
    }
}
