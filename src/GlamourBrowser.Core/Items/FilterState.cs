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
    public uint? ClassJobId { get; init; }
    public int MinLevel { get; init; } = 1;
    public int MaxLevel { get; init; } = 100;
    public IReadOnlySet<int> DyeCounts { get; init; } = new HashSet<int>();
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;
    public bool WearableOnly { get; init; } = true;
}
