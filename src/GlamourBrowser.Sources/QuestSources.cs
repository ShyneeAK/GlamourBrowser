using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Sources;
using Lumina;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

/// <summary>Quest rewards (incl. seasonal events and job quests), achievements and relic steps.</summary>
public static class QuestSources
{
    public static void AddQuests(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant)
    {
        foreach (var quest in data.GetExcelSheet<Quest>()!)
        {
            if (quest.Name.IsEmpty)
                continue;
            var genre = quest.JournalGenre.IsValid ? quest.JournalGenre.Value : default;
            var category = quest.JournalGenre.IsValid ? QuestCategory.From(genre.JournalCategory.RowId) : SourceCategory.Quest;
            var detail = quest.JournalGenre.IsValid ? genre.Name.ExtractText() : Loc.T("Quest");
            var title = quest.Name.ExtractText();

            foreach (var reward in quest.Reward)
            {
                if (relevant.Contains(reward.RowId))
                    builder.Add(reward.RowId, new ItemSource(category, title, detail));
            }
            foreach (var reward in quest.OptionalItemReward)
            {
                if (relevant.Contains(reward.RowId))
                    builder.Add(reward.RowId, new ItemSource(category, title, Loc.F("{0} · Auswahl", detail)));
            }
        }
    }

    public static void AddAchievements(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant)
    {
        foreach (var achievement in data.GetExcelSheet<Achievement>()!)
        {
            if (relevant.Contains(achievement.Item.RowId))
                builder.Add(achievement.Item.RowId, new ItemSource(SourceCategory.Achievement, achievement.Name.ExtractText(), Loc.T("Belohnung")));
        }
    }

    public static void AddRelics(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        foreach (var relic in SupplementalCsv.Load<RelicWeapon>(data, CsvLoader.RelicWeaponResourceName, log))
        {
            foreach (var itemId in new[] { relic.ItemId, relic.OffhandItemId })
            {
                if (relevant.Contains(itemId))
                    builder.Add(itemId, new ItemSource(SourceCategory.JobGear, Loc.T("Reliktwaffe"), Loc.F("Stufe {0}", relic.Stage)));
            }
        }
        foreach (var relic in SupplementalCsv.Load<RelicTool>(data, CsvLoader.RelicToolResourceName, log))
        {
            if (relevant.Contains(relic.ItemId))
                builder.Add(relic.ItemId, new ItemSource(SourceCategory.JobGear, Loc.T("Reliktwerkzeug"), Loc.F("Stufe {0}", relic.Stage)));
        }
    }
}
