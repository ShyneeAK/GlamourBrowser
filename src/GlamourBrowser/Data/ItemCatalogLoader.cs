using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GlamourBrowser.Core.Items;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Data;

/// <summary>Builds the item catalog from Lumina: every equippable item that has a model.</summary>
public static class ItemCatalogLoader
{
    public static IReadOnlyList<GlamourItem> Load(IDataManager data)
    {
        var jobsByCategory = BuildJobsByCategory(data);
        var restrictions = new Dictionary<uint, RaceGenderRestriction>();
        var result = new List<GlamourItem>();

        foreach (var item in data.GetExcelSheet<Item>())
        {
            if (item.EquipSlotCategory.RowId == 0 || item.ModelMain == 0 || item.Name.IsEmpty)
                continue;
            if (SlotOf(item.EquipSlotCategory.Value) is not { } slot)
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
                Tradabilities.From(item.IsUntradable, item.ItemSearchCategory.RowId)));
        }

        Plugin.Log.Information($"Item catalog: {result.Count} items");
        return result;
    }

    /// <summary>The first slot the item occupies (value 1); -1 marks slots it only blocks.</summary>
    private static EquipSlot? SlotOf(EquipSlotCategory c)
    {
        if (c.MainHand == 1) return EquipSlot.MainHand;
        if (c.OffHand == 1) return EquipSlot.OffHand;
        if (c.Head == 1) return EquipSlot.Head;
        if (c.Body == 1) return EquipSlot.Body;
        if (c.Gloves == 1) return EquipSlot.Hands;
        if (c.Legs == 1) return EquipSlot.Legs;
        if (c.Feet == 1) return EquipSlot.Feet;
        if (c.Ears == 1) return EquipSlot.Ears;
        if (c.Neck == 1) return EquipSlot.Neck;
        if (c.Wrists == 1) return EquipSlot.Wrists;
        if (c.FingerL == 1 || c.FingerR == 1) return EquipSlot.RingRight;
        return null;
    }

    /// <summary>
    /// ClassJobCategory has one bool column per job, named like the English ClassJob.Abbreviation.
    /// The abbreviation is localized, so the English sheet is used regardless of client language.
    /// </summary>
    private static Dictionary<uint, IReadOnlySet<uint>> BuildJobsByCategory(IDataManager data)
    {
        var columns = data.GetExcelSheet<ClassJob>(ClientLanguage.English)
            .Select(job => (job.RowId, Property: typeof(ClassJobCategory).GetProperty(job.Abbreviation.ExtractText(), BindingFlags.Public | BindingFlags.Instance)))
            .Where(x => x.Property?.PropertyType == typeof(bool))
            .ToList();

        var result = new Dictionary<uint, IReadOnlySet<uint>>();
        foreach (var category in data.GetExcelSheet<ClassJobCategory>())
        {
            object boxed = category;
            result[category.RowId] = columns
                .Where(x => (bool)x.Property!.GetValue(boxed)!)
                .Select(x => x.RowId)
                .ToHashSet();
        }
        return result;
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
