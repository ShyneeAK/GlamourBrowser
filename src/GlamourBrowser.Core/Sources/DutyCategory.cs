namespace GlamourBrowser.Core.Sources;

public static class DutyCategory
{
    /// <summary>ContentType: 2 dungeons, 4 trials, 5 raids, 28 ultimate raids, 37 chaotic alliance raid; everything else is other content.</summary>
    public static SourceCategory From(uint contentTypeId) => contentTypeId switch
    {
        2 => SourceCategory.Dungeon,
        4 => SourceCategory.Trial,
        5 or 28 or 37 => SourceCategory.Raid,
        _ => SourceCategory.OtherContent,
    };
}
