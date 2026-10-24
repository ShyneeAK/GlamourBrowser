using Lumina;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

internal static class SupplementalCsv
{
    /// <summary>Loads one LuminaSupplemental resource; skipped lines are logged.</summary>
    public static List<T> Load<T>(GameData data, string resource, Action<string> log) where T : LuminaSupplemental.Excel.Model.ICsv, new()
    {
        var rows = CsvLoader.LoadResource<T>(resource, true, out var failed, out _, data, null);
        if (failed.Count > 0)
            log($"{typeof(T).Name}: {failed.Count} lines skipped");
        return rows;
    }
}
