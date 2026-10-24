namespace GlamourBrowser.Core.Sources;

public static class QuestCategory
{
    /// <summary>JournalCategory: 97 seasonal events; 93/94 job quests and 56 weapon enhancement count as job artifact.</summary>
    public static SourceCategory From(uint journalCategoryId) => journalCategoryId switch
    {
        97 => SourceCategory.SeasonalEvent,
        93 or 94 or 56 => SourceCategory.JobGear,
        _ => SourceCategory.Quest,
    };
}
