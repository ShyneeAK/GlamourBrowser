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

    /// <summary>Gear a duty gives through a coffer (ItemSupplement) or a token (SpecialShop cost), one step deep.</summary>
    public static void AddDutyChains(SourceIndex.Builder builder, GameData data, IReadOnlySet<uint> relevant, Action<string> log)
    {
        var duties = data.GetExcelSheet<ContentFinderCondition>()!;
        var englishDuties = data.GetExcelSheet<ContentFinderCondition>(Language.English);
        var items = data.GetExcelSheet<Item>()!;

        var drops = new List<(uint DutyId, uint ItemId)>();
        foreach (var drop in SupplementalCsv.Load<DungeonBossDrop>(data, CsvLoader.DungeonBossDropResourceName, log))
            drops.Add((drop.ContentFinderConditionId, drop.ItemId));
        var chests = SupplementalCsv.Load<DungeonChest>(data, CsvLoader.DungeonChestResourceName, log).ToDictionary(c => c.RowId);
        foreach (var item in SupplementalCsv.Load<DungeonChestItem>(data, CsvLoader.DungeonChestItemResourceName, log))
        {
            if (chests.TryGetValue(item.ChestId, out var chest))
                drops.Add((chest.ContentFinderConditionId, item.ItemId));
        }
        foreach (var drop in SupplementalCsv.Load<DungeonDrop>(data, CsvLoader.DungeonDropItemResourceName, log))
            drops.Add((drop.ContentFinderConditionId, drop.ItemId));

        // Only opening something counts; desynthesis, reduction or gardening results are not what a duty "gives".
        var coffers = SupplementalCsv.Load<ItemSupplement>(data, CsvLoader.ItemSupplementResourceName, log)
            .Where(s => s.ItemSupplementSource is ItemSupplementSource.Loot or ItemSupplementSource.Coffer)
            .GroupBy(s => s.SourceItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<uint>)g.Select(s => s.ItemId).ToList());

        var tokens = new Dictionary<uint, List<uint>>();
        foreach (var shop in data.GetExcelSheet<SpecialShop>()!)
        {
            foreach (var entry in shop.Item)
            {
                var received = new List<uint>();
                foreach (var receive in entry.ReceiveItems)
                {
                    if (relevant.Contains(receive.Item.RowId))
                        received.Add(receive.Item.RowId);
                }
                if (received.Count == 0)
                    continue;
                foreach (var cost in entry.ItemCosts)
                {
                    // Ids 1–7 are currency codes, not items a duty could drop.
                    if (cost.ItemCost.RowId <= 7)
                        continue;
                    if (!tokens.TryGetValue(cost.ItemCost.RowId, out var list))
                        tokens[cost.ItemCost.RowId] = list = new List<uint>();
                    list.AddRange(received.Where(r => !list.Contains(r)));
                }
            }
        }
        var tokenRewards = tokens.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<uint>)kv.Value);

        foreach (var chain in DutyChains.Resolve(drops, coffers, tokenRewards, relevant))
        {
            if (!duties.TryGetRow(chain.DutyId, out var cfc))
                continue;
            var via = items.TryGetRow(chain.ViaItemId, out var viaItem) ? viaItem.Name.ExtractText() : "";
            var detail = chain.Kind == ChainKind.Coffer ? $"aus {via}" : $"über {via}";
            builder.Add(chain.ItemId, new ItemSource(CategoryOf(cfc, englishDuties), Capitalize(cfc.Name.ExtractText()), detail, new DutyFinder(chain.DutyId)));
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
