using GlamourBrowser.Core.Sources;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

/// <summary>Duty drops and chests (dungeons, trials, raids, other content) and field operation coffers.</summary>
public static class ContentSources
{
    public static void AddDuties(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        var duties = data.GetExcelSheet<ContentFinderCondition>()!;
        var englishDuties = data.GetExcelSheet<ContentFinderCondition>(Language.English);

        void Add(uint itemId, uint cfcId, string detail)
        {
            if (!relevant.Contains(itemId) || !duties.TryGetRow(cfcId, out var cfc))
                return;
            builder.Add(itemId, new ItemSource(CategoryOf(cfc, englishDuties), Capitalize(cfc.Name.ExtractText()), detail, new DutyFinder(cfcId)));
        }

        foreach (var drop in SupplementalCsv.Load<DungeonBossDrop>(data, CsvLoader.DungeonBossDropResourceName, log))
            Add(drop.ItemId, drop.ContentFinderConditionId, $"Boss {drop.FightNo}");

        var chests = SupplementalCsv.Load<DungeonChest>(data, CsvLoader.DungeonChestResourceName, log).ToDictionary(c => c.RowId);
        foreach (var item in SupplementalCsv.Load<DungeonChestItem>(data, CsvLoader.DungeonChestItemResourceName, log))
        {
            if (chests.TryGetValue(item.ChestId, out var chest))
                Add(item.ItemId, chest.ContentFinderConditionId, $"Truhe {chest.ChestNo}");
        }

        foreach (var drop in SupplementalCsv.Load<DungeonDrop>(data, CsvLoader.DungeonDropItemResourceName, log))
            Add(drop.ItemId, drop.ContentFinderConditionId, "Drop");

        foreach (var coffer in SupplementalCsv.Load<FieldOpCoffer>(data, CsvLoader.FieldOpCofferResourceName, log))
        {
            if (relevant.Contains(coffer.ItemId))
                builder.Add(coffer.ItemId, new ItemSource(SourceCategory.OtherContent, "Field Operation", "Schatzkiste"));
        }
    }

    internal static SourceCategory CategoryOf(ContentFinderCondition cfc, Lumina.Excel.ExcelSheet<ContentFinderCondition>? english)
    {
        var englishName = english != null && english.TryGetRow(cfc.RowId, out var en) ? en.Name.ExtractText() : "";
        return DutyCategory.From(cfc.ContentType.RowId, cfc.ContentMemberType.RowId, englishName, cfc.Name.ExtractText());
    }

    // German duty names may start in lower case ("sastasha").
    internal static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
