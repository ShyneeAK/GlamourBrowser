using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Sources;
using Lumina;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

/// <summary>Recipes, Mog Station, FATEs, monster drops, coffers, ventures and the patch per item.</summary>
public static class MiscSources
{
    private const uint FirstCrafterJob = 8; // CraftType 0 = Carpenter = ClassJob 8

    public static void AddRecipes(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant)
    {
        var jobs = data.GetExcelSheet<ClassJob>()!;
        foreach (var recipe in data.GetExcelSheet<Recipe>()!)
        {
            var itemId = recipe.ItemResult.RowId;
            if (!relevant.Contains(itemId))
                continue;
            var job = jobs.TryGetRow(FirstCrafterJob + recipe.CraftType.RowId, out var classJob) ? ContentSources.Capitalize(classJob.Name.ExtractText()) : Loc.T("Handwerk");
            var level = recipe.RecipeLevelTable.IsValid ? recipe.RecipeLevelTable.Value.ClassJobLevel : 0;
            builder.Add(itemId, new ItemSource(SourceCategory.Crafting, job, Loc.F("Lv. {0}", level), new OpenRecipe(recipe.RowId)));
        }
    }

    public static void AddMogStation(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        foreach (var item in SupplementalCsv.Load<StoreItem>(data, CsvLoader.StoreItemResourceName, log))
        {
            if (relevant.Contains(item.ItemId))
                builder.Add(item.ItemId, new ItemSource(SourceCategory.MogStation, Loc.T("Mogstation"), item.PriceCentsUSD > 0 ? $"{item.PriceCentsUSD / 100.0:0.00} $" : Loc.T("Shop"),
                    item.StoreId != 0 ? new OpenStore(item.StoreId) : null));
        }
    }

    public static void AddMisc(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        var fates = data.GetExcelSheet<Fate>()!;
        foreach (var row in SupplementalCsv.Load<FateItem>(data, CsvLoader.FateItemResourceName, log))
        {
            if (relevant.Contains(row.ItemId))
                builder.Add(row.ItemId, new ItemSource(SourceCategory.Other, Loc.T("FATE"), fates.TryGetRow(row.FateId, out var fate) ? fate.Name.ExtractText() : ""));
        }

        var monsters = data.GetExcelSheet<BNpcName>()!;
        foreach (var row in SupplementalCsv.Load<MobDrop>(data, CsvLoader.MobDropResourceName, log))
        {
            if (relevant.Contains(row.ItemId))
                builder.Add(row.ItemId, new ItemSource(SourceCategory.Other, Loc.T("Monster"), monsters.TryGetRow(row.BNpcNameId, out var name) ? ContentSources.Capitalize(name.Singular.ExtractText()) : ""));
        }

        var items = data.GetExcelSheet<Item>()!;
        foreach (var row in SupplementalCsv.Load<ItemSupplement>(data, CsvLoader.ItemSupplementResourceName, log))
        {
            if (relevant.Contains(row.ItemId))
                builder.Add(row.ItemId, new ItemSource(SourceCategory.Other, Loc.T("Gewinnung"), items.TryGetRow(row.SourceItemId, out var source) ? Loc.F("aus {0}", source.Name.ExtractText()) : ""));
        }

        foreach (var row in SupplementalCsv.Load<RetainerVentureItem>(data, CsvLoader.RetainerVentureItemResourceName, log))
        {
            if (relevant.Contains(row.ItemId))
                builder.Add(row.ItemId, new ItemSource(SourceCategory.Other, Loc.T("Kundschafter"), Loc.T("Unternehmung")));
        }
    }

    public static void AddPatches(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        var sorted = relevant.Order().ToArray();
        foreach (var range in SupplementalCsv.Load<ItemPatch>(data, CsvLoader.ItemPatchResourceName, log))
        {
            var start = Array.BinarySearch(sorted, range.StartItemId);
            if (start < 0)
                start = ~start;
            for (var i = start; i < sorted.Length && sorted[i] <= range.EndItemId; i++)
                builder.SetPatch(sorted[i], (float)range.PatchNo);
        }
    }
}
