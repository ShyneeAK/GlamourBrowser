using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Items;

public enum OwnershipFilter
{
    All,
    Owned,
    Missing,
}

/// <summary>Filter selection. Empty sets mean "no restriction".</summary>
public sealed record FilterState
{
    public string SearchText { get; init; } = "";
    public IReadOnlySet<EquipSlot> Slots { get; init; } = new HashSet<EquipSlot>();

    /// <summary>What the grid shows; the slots only apply to <see cref="BrowseMode.Items"/>.</summary>
    public BrowseMode Mode { get; init; }
    /// <summary>Gear made for these jobs (see <see cref="JobFilter"/>); empty means every item.</summary>
    public IReadOnlySet<uint> Jobs { get; init; } = new HashSet<uint>();
    public int MinLevel { get; init; } = 1;
    public int MaxLevel { get; init; } = 100;
    /// <summary>Only items with at least one dye channel ("Nur Färbbare").</summary>
    public bool DyeableOnly { get; init; }
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;
    public bool WearableOnly { get; init; } = true;
    public IReadOnlySet<SourceCategory> Categories { get; init; } = new HashSet<SourceCategory>();
    public IReadOnlySet<Expansion> Expansions { get; init; } = new HashSet<Expansion>();
    public SortOrder Sort { get; init; } = SortOrder.Level;
}
