using System.Collections.Generic;
using Dalamud.Interface;
using GlamourBrowser.Core.Sources;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

/// <summary>Game icon per source category (content types, currency items, job icons), FontAwesome otherwise.</summary>
public static class SourceIcons
{
    private static readonly Dictionary<SourceCategory, uint> Cache = new();

    public static uint IconFor(SourceCategory category)
    {
        if (Cache.TryGetValue(category, out var icon))
            return icon;

        icon = category switch
        {
            SourceCategory.Dungeon => ContentTypeIcon(2),
            SourceCategory.Trial => ContentTypeIcon(4),
            SourceCategory.RaidNormal or SourceCategory.RaidSavage => ContentTypeIcon(5),
            SourceCategory.RaidUltimate => ContentTypeIcon(28),
            SourceCategory.RaidAlliance => ContentTypeIcon(37),
            SourceCategory.OtherContent => ContentTypeIcon(21),
            SourceCategory.Tomestones => ItemIcon(28),
            SourceCategory.Scrips => ItemIcon(25199),
            SourceCategory.Tokens => ItemIcon(26807),
            SourceCategory.GrandCompany => ItemIcon(20),
            SourceCategory.GoldSaucer => ItemIcon(29),
            SourceCategory.PvP => ItemIcon(25),
            SourceCategory.GilVendor => ItemIcon(1),
            SourceCategory.Crafting => 62108,     // Carpenter
            SourceCategory.JobArtifact => 62119,  // Paladin
            _ => 0,
        };
        return Cache[category] = icon;
    }

    /// <summary>The game has no own savage icon: savage uses the raid icon with a red corner the UI draws on top.</summary>
    public static bool HasSavageMark(SourceCategory category) => category == SourceCategory.RaidSavage;

    public static FontAwesomeIcon Fallback(SourceCategory category) => category switch
    {
        SourceCategory.Achievement => FontAwesomeIcon.Trophy,
        SourceCategory.Quest => FontAwesomeIcon.Exclamation,
        SourceCategory.SeasonalEvent => FontAwesomeIcon.Gift,
        SourceCategory.MogStation => FontAwesomeIcon.ShoppingCart,
        _ => FontAwesomeIcon.Question,
    };

    /// <summary>Icon of any item, cached (used for cost currencies).</summary>
    public static uint ItemIcon(uint itemId)
        => Plugin.DataManager.GetExcelSheet<Item>().TryGetRow(itemId, out var item) ? item.Icon : 0u;

    private static uint ContentTypeIcon(uint row)
        => Plugin.DataManager.GetExcelSheet<ContentType>().TryGetRow(row, out var type) ? type.Icon : 0u;
}
