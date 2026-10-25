using Lumina;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

public static class CofferContents
{
    /// <summary>Coffers (and other items opened for loot) mapped to the relevant items they contain.</summary>
    public static IReadOnlyDictionary<uint, IReadOnlyList<uint>> Find(GameData data, IReadOnlySet<uint> relevant, Action<string> log)
        => SupplementalCsv.Load<ItemSupplement>(data, CsvLoader.ItemSupplementResourceName, log)
            .Where(s => s.ItemSupplementSource is ItemSupplementSource.Loot or ItemSupplementSource.Coffer && relevant.Contains(s.ItemId))
            .GroupBy(s => s.SourceItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<uint>)g.Select(s => s.ItemId).Distinct().ToList());
}
