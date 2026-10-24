namespace GlamourBrowser.Core.Sources;

public enum SourceGroup
{
    Content,
    Quest,
    Currency,
    Vendor,
}

/// <summary>Which sources a tile shows: one icon per group, each the highest ranked category the item has.</summary>
public static class SourceRanking
{
    private static readonly SourceCategory[][] Groups =
    [
        [SourceCategory.RaidSavage, SourceCategory.RaidUltimate, SourceCategory.RaidNormal, SourceCategory.RaidAlliance,
         SourceCategory.Trial, SourceCategory.Dungeon, SourceCategory.OtherContent],
        [SourceCategory.Quest, SourceCategory.JobGear, SourceCategory.Achievement, SourceCategory.SeasonalEvent, SourceCategory.Crafting],
        [SourceCategory.Tomestones, SourceCategory.Scrips, SourceCategory.Tokens, SourceCategory.GrandCompany,
         SourceCategory.GoldSaucer, SourceCategory.PvP],
        [SourceCategory.GilVendor, SourceCategory.MogStation, SourceCategory.Other],
    ];

    public static IReadOnlyList<SourceCategory> Icons(IReadOnlySet<SourceCategory> categories)
    {
        var result = new List<SourceCategory>(Groups.Length);
        foreach (var group in Groups)
        {
            foreach (var category in group)
            {
                if (categories.Contains(category))
                {
                    result.Add(category);
                    break;
                }
            }
        }
        return result;
    }

    public static SourceCategory? Top(IReadOnlySet<SourceCategory> categories)
    {
        var icons = Icons(categories);
        return icons.Count > 0 ? icons[0] : null;
    }
}
