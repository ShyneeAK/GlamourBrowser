using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Ownership;

/// <summary>Where an item can be stored. The order is the display order of locations.</summary>
public enum ContainerKind
{
    Inventory,
    Equipped,
    Armoury,
    Saddlebag,
    PremiumSaddlebag,
    Retainer,
    Dresser,
    Armoire,
}

public static class ContainerNames
{
    /// <summary>A container's name in the loaded language.</summary>
    /// <param name="name">A retainer's own name ("Mira"); empty for the other containers.</param>
    public static string DisplayName(ContainerKind kind, string name) => kind == ContainerKind.Retainer
        ? Loc.F("Gehilfe {0}", name)
        : Loc.T(kind switch
        {
            ContainerKind.Inventory => "Inventar",
            ContainerKind.Equipped => "Ausgerüstet",
            ContainerKind.Armoury => "Arsenal",
            ContainerKind.Saddlebag => "Satteltasche",
            ContainerKind.PremiumSaddlebag => "Satteltasche (Premium)",
            ContainerKind.Dresser => "Kommode",
            _ => "Armoire",
        });
}
