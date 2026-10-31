using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Sources;

/// <summary>
/// Where an item comes from. The values are kept in the configuration (saved filter), so new categories go at the end;
/// <see cref="SourceCategories.All"/> holds the display order.
/// </summary>
public enum SourceCategory
{
    Crafting,
    Dungeon,
    Trial,
    RaidNormal,
    RaidSavage,
    RaidUltimate,
    RaidAlliance,
    OtherContent,
    Tomestones,
    Scrips,
    Tokens,
    GrandCompany,
    GoldSaucer,
    Achievement,
    SeasonalEvent,
    Quest,
    JobGear,
    GilVendor,
    PvP,
    MogStation,
    Other,
    Hunt,
}

public static class SourceCategories
{
    private static readonly SourceCategory[] Ordered =
    [
        SourceCategory.Crafting, SourceCategory.Dungeon, SourceCategory.Trial, SourceCategory.RaidNormal, SourceCategory.RaidSavage,
        SourceCategory.RaidUltimate, SourceCategory.RaidAlliance, SourceCategory.OtherContent, SourceCategory.Tomestones,
        SourceCategory.Scrips, SourceCategory.Tokens, SourceCategory.Hunt, SourceCategory.GrandCompany, SourceCategory.GoldSaucer,
        SourceCategory.Achievement, SourceCategory.SeasonalEvent, SourceCategory.Quest, SourceCategory.JobGear,
        SourceCategory.GilVendor, SourceCategory.PvP, SourceCategory.MogStation, SourceCategory.Other,
    ];

    /// <summary>Every category in display order.</summary>
    public static IReadOnlyList<SourceCategory> All => Ordered;

    /// <summary>Position in <see cref="All"/>, for sorting by display order.</summary>
    public static int Order(SourceCategory category) => Array.IndexOf(Ordered, category);

    public static string DisplayName(SourceCategory category) => Loc.T(category switch
    {
        SourceCategory.Crafting => "Handwerk",
        SourceCategory.Dungeon => "Dungeon",
        SourceCategory.Trial => "Prüfung",
        SourceCategory.RaidNormal => "Raid (Normal)",
        SourceCategory.RaidSavage => "Raid (Episch)",
        SourceCategory.RaidUltimate => "Fatal-Raid",
        SourceCategory.RaidAlliance => "Allianz-Raid",
        SourceCategory.OtherContent => "Sonstige Inhalte",
        SourceCategory.Tomestones => "Tomestones",
        SourceCategory.Scrips => "Scrips",
        SourceCategory.Tokens => "Marken & Token",
        SourceCategory.Hunt => "Hunt",
        SourceCategory.GrandCompany => "Staatliche Gesellschaft",
        SourceCategory.GoldSaucer => "Gold Saucer",
        SourceCategory.Achievement => "Errungenschaft",
        SourceCategory.SeasonalEvent => "Saison-Event",
        SourceCategory.Quest => "Quest",
        SourceCategory.JobGear => "Job-Ausrüstung",
        SourceCategory.GilVendor => "Händler (Gil)",
        SourceCategory.PvP => "PvP",
        SourceCategory.MogStation => "Mogstation",
        _ => "Sonstiges",
    });
}
