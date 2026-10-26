using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Sources;

public enum Expansion
{
    ARealmReborn,
    Heavensward,
    Stormblood,
    Shadowbringers,
    Endwalker,
    Dawntrail,
}

public static class Expansions
{
    /// <summary>Major version 2–7 maps to ARR–DT, later versions to the newest known; 0 (unknown) gives null.</summary>
    public static Expansion? FromPatch(float patch)
    {
        if (patch <= 0)
            return null;
        var major = (int)MathF.Floor(patch);
        return (Expansion)Math.Clamp(major - 2, 0, (int)Expansion.Dawntrail);
    }

    public static string DisplayName(Expansion expansion) => expansion switch
    {
        Expansion.ARealmReborn => "ARR",
        Expansion.Heavensward => "HW",
        Expansion.Stormblood => "SB",
        Expansion.Shadowbringers => "ShB",
        Expansion.Endwalker => "EW",
        _ => "DT",
    };

    public static string LongName(Expansion expansion) => Loc.T(expansion switch
    {
        Expansion.ARealmReborn => "A Realm Reborn",
        Expansion.Heavensward => "Heavensward",
        Expansion.Stormblood => "Stormblood",
        Expansion.Shadowbringers => "Shadowbringers",
        Expansion.Endwalker => "Endwalker",
        _ => "Dawntrail",
    });
}
