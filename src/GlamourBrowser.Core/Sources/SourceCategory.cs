namespace GlamourBrowser.Core.Sources;

/// <summary>Where an item comes from, in display order.</summary>
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
    JobArtifact,
    GilVendor,
    PvP,
    MogStation,
    Other,
}

public static class SourceCategories
{
    public static string DisplayName(SourceCategory category) => category switch
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
        SourceCategory.GrandCompany => "Staatliche Gesellschaft",
        SourceCategory.GoldSaucer => "Gold Saucer",
        SourceCategory.Achievement => "Errungenschaft",
        SourceCategory.SeasonalEvent => "Saison-Event",
        SourceCategory.Quest => "Quest",
        SourceCategory.JobArtifact => "Job-Artefakt",
        SourceCategory.GilVendor => "Händler (Gil)",
        SourceCategory.PvP => "PvP",
        SourceCategory.MogStation => "Mogstation",
        _ => "Sonstiges",
    };
}
