using System.Reflection;
using GlamourBrowser.Core.Ui;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

/// <summary>Armour and accessories wearable by exactly one job (plus its base class): their vendor offers count as job gear.</summary>
public static class JobGearItems
{
    public static IReadOnlySet<uint> Find(GameData data, IReadOnlySet<uint> relevant)
    {
        // ClassJobCategory has one bool column per job, named like the English ClassJob.Abbreviation.
        var classJobs = data.GetExcelSheet<ClassJob>(Language.English)!;
        var parents = classJobs.ToDictionary(j => j.RowId, j => j.ClassJobParent.RowId);
        var columns = classJobs
            .Select(j => (j.RowId, Property: typeof(ClassJobCategory).GetProperty(j.Abbreviation.ExtractText(), BindingFlags.Public | BindingFlags.Instance)))
            .Where(x => x.Property?.PropertyType == typeof(bool))
            .ToList();

        var singleJob = new HashSet<uint>();
        foreach (var category in data.GetExcelSheet<ClassJobCategory>()!)
        {
            object boxed = category;
            var jobs = columns.Where(x => (bool)x.Property!.GetValue(boxed)!).Select(x => x.RowId).ToHashSet();
            if (JobBadges.IsSingleJob(jobs, id => parents.GetValueOrDefault(id, id)))
                singleJob.Add(category.RowId);
        }

        var items = data.GetExcelSheet<Item>()!;
        // Weapons are single-job by nature; only armour and accessories made for one job count as job gear.
        return relevant.Where(id => items.TryGetRow(id, out var item)
                                    && singleJob.Contains(item.ClassJobCategory.RowId)
                                    && item.EquipSlotCategory.Value.MainHand == 0 && item.EquipSlotCategory.Value.OffHand == 0).ToHashSet();
    }
}
