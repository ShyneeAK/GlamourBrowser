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

    /// <summary>The grid shows whole sets instead of single items (the "Ausrüstungs-Sets" entry of the slot bar).</summary>
    public bool Sets { get; init; }

    /// <summary>The grid shows the wishlist (the star of the slot bar); only the search applies.</summary>
    public bool Wishlist { get; init; }
    public uint? ClassJobId { get; init; }
    public int MinLevel { get; init; } = 1;
    public int MaxLevel { get; init; } = 100;
    /// <summary>Only items with at least one dye channel ("Färbbar").</summary>
    public bool DyeableOnly { get; init; }
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;
    public bool WearableOnly { get; init; } = true;
    public IReadOnlySet<SourceCategory> Categories { get; init; } = new HashSet<SourceCategory>();
    public IReadOnlySet<Expansion> Expansions { get; init; } = new HashSet<Expansion>();
    public SortOrder Sort { get; init; } = SortOrder.Level;
}
