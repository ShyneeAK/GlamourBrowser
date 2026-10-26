using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Ui;

/// <summary>Job groups in display order.</summary>
public enum JobGroup
{
    Tank,
    Healer,
    Melee,
    Ranged,
    Magic,
    Crafter,
    Gatherer,
}

public static class JobGroups
{
    private const uint CrafterCategory = 33;
    private const uint GathererCategory = 32;
    private const byte Intelligence = 4;

    /// <summary>
    /// Group from ClassJob data: category 33/32 are crafters/gatherers; role 1 tank, 4 healer, 2 melee;
    /// role 3 is magic when the primary stat is intelligence, otherwise physical ranged.
    /// </summary>
    public static JobGroup GroupOf(byte role, uint classJobCategory, byte primaryStat)
    {
        if (classJobCategory == CrafterCategory)
            return JobGroup.Crafter;
        if (classJobCategory == GathererCategory)
            return JobGroup.Gatherer;

        return role switch
        {
            1 => JobGroup.Tank,
            4 => JobGroup.Healer,
            2 => JobGroup.Melee,
            _ => primaryStat == Intelligence ? JobGroup.Magic : JobGroup.Ranged,
        };
    }

    public static string DisplayName(JobGroup group) => Loc.T(group switch
    {
        JobGroup.Tank => "Tank",
        JobGroup.Healer => "Heiler",
        JobGroup.Melee => "Nahkampf",
        JobGroup.Ranged => "Fernkampf",
        JobGroup.Magic => "Magie",
        JobGroup.Crafter => "Handwerk",
        _ => "Sammeln",
    });
}
