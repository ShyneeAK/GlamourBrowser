using System.Reflection;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

public static class ClassJobCategories
{
    /// <summary>
    /// The jobs of every ClassJobCategory row. The sheet has one bool column per job, named like the English
    /// ClassJob.Abbreviation; the abbreviation is localized, so the English sheet is used regardless of language.
    /// </summary>
    public static IReadOnlyDictionary<uint, IReadOnlySet<uint>> JobsByCategory(GameData data)
    {
        var columns = data.GetExcelSheet<ClassJob>(Language.English)!
            .Select(job => (job.RowId, Property: typeof(ClassJobCategory).GetProperty(job.Abbreviation.ExtractText(), BindingFlags.Public | BindingFlags.Instance)))
            .Where(x => x.Property?.PropertyType == typeof(bool))
            .ToList();

        var result = new Dictionary<uint, IReadOnlySet<uint>>();
        foreach (var category in data.GetExcelSheet<ClassJobCategory>()!)
        {
            object boxed = category;
            result[category.RowId] = columns.Where(x => (bool)x.Property!.GetValue(boxed)!).Select(x => x.RowId).ToHashSet();
        }
        return result;
    }
}
