using GlamourBrowser.Core.Ui;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

/// <summary>Items wearable by exactly one job (plus its base class), weapons included: their vendor offers count as job gear.</summary>
public static class JobGearItems
{
    public static IReadOnlySet<uint> Find(GameData data, IReadOnlySet<uint> relevant)
    {
        var parents = data.GetExcelSheet<ClassJob>(Language.English)!.ToDictionary(j => j.RowId, j => j.ClassJobParent.RowId);
        var singleJob = ClassJobCategories.JobsByCategory(data)
            .Where(kv => JobBadges.IsSingleJob(kv.Value, id => parents.GetValueOrDefault(id, id)))
            .Select(kv => kv.Key)
            .ToHashSet();

        var items = data.GetExcelSheet<Item>()!;
        return relevant.Where(id => items.TryGetRow(id, out var item) && singleJob.Contains(item.ClassJobCategory.RowId)).ToHashSet();
    }
}
