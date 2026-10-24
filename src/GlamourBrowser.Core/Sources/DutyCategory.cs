namespace GlamourBrowser.Core.Sources;

public static class DutyCategory
{
    private const uint AllianceMembers = 4;

    /// <summary>
    /// ContentType 2 dungeons, 4 trials, 28 ultimate raids, 37 chaotic alliance raids, 5 raids; everything else is other content.
    /// Raids with 24 players (ContentMemberType 4) are alliance raids; savage raids are recognised by the English name,
    /// with "(episch)" in the German name as fallback when the English name is missing.
    /// </summary>
    public static SourceCategory From(uint contentTypeId, uint contentMemberTypeId, string englishName, string germanName) => contentTypeId switch
    {
        2 => SourceCategory.Dungeon,
        4 => SourceCategory.Trial,
        28 => SourceCategory.RaidUltimate,
        37 => SourceCategory.RaidAlliance,
        5 when contentMemberTypeId == AllianceMembers => SourceCategory.RaidAlliance,
        5 when IsSavage(englishName, germanName) => SourceCategory.RaidSavage,
        5 => SourceCategory.RaidNormal,
        _ => SourceCategory.OtherContent,
    };

    private static bool IsSavage(string englishName, string germanName)
        => englishName.Length > 0
            ? englishName.Contains("(Savage)", StringComparison.OrdinalIgnoreCase)
            : germanName.Contains("(episch)", StringComparison.OrdinalIgnoreCase);
}
