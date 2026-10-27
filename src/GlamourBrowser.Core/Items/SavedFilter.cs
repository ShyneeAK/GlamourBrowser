using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Items;

/// <summary>
/// The filter as it is kept in the configuration, so the browser opens after a restart where it was left: the view (mode
/// and slots) and every filter, but not the search, which is typed for the moment.
/// </summary>
public sealed class SavedFilter
{
    public BrowseMode Mode { get; set; }
    public List<EquipSlot> Slots { get; set; } = [];
    public List<uint> Jobs { get; set; } = [];
    public bool JobsExclusive { get; set; }
    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 100;
    public bool DyeableOnly { get; set; }
    public List<WristSide> Wrists { get; set; } = [];
    public byte Dye1 { get; set; }
    public byte Dye2 { get; set; }
    public OwnershipFilter Ownership { get; set; }
    public bool WearableOnly { get; set; } = true;
    public List<SourceCategory> Categories { get; set; } = [];
    public List<Expansion> Expansions { get; set; } = [];

    public static SavedFilter From(FilterState f) => new()
    {
        Mode = f.Mode,
        Slots = f.Slots.Order().ToList(),
        Jobs = f.Jobs.Order().ToList(),
        JobsExclusive = f.JobsExclusive,
        MinLevel = f.MinLevel,
        MaxLevel = f.MaxLevel,
        DyeableOnly = f.DyeableOnly,
        Wrists = f.Wrists.Order().ToList(),
        Dye1 = f.Dye.First,
        Dye2 = f.Dye.Second,
        Ownership = f.Ownership,
        WearableOnly = f.WearableOnly,
        Categories = f.Categories.Order().ToList(),
        Expansions = f.Expansions.Order().ToList(),
    };

    /// <summary>The filter again; values a newer or hand-edited configuration has but this version does not know are left out.</summary>
    public FilterState ToState()
    {
        static HashSet<T> Known<T>(IEnumerable<T>? values) where T : struct, Enum => (values ?? []).Where(v => Enum.IsDefined(v)).ToHashSet();
        var min = Math.Clamp(MinLevel, 1, 100);
        return new FilterState
        {
            Mode = Enum.IsDefined(Mode) ? Mode : BrowseMode.Items,
            Slots = Known(Slots),
            Jobs = (Jobs ?? []).ToHashSet(),
            JobsExclusive = JobsExclusive,
            MinLevel = min,
            MaxLevel = Math.Clamp(MaxLevel, min, 100),
            DyeableOnly = DyeableOnly,
            Wrists = Known(Wrists),
            Dye = new DyePair(Dye1, Dye2),
            Ownership = Enum.IsDefined(Ownership) ? Ownership : OwnershipFilter.All,
            WearableOnly = WearableOnly,
            Categories = Known(Categories),
            Expansions = Known(Expansions),
        };
    }

    /// <summary>Whether two filters keep the same (everything but the search and the sort, which the config holds itself).</summary>
    public static bool SameKept(FilterState a, FilterState b)
        => a.Mode == b.Mode && a.Slots.SetEquals(b.Slots) && a.Jobs.SetEquals(b.Jobs) && a.JobsExclusive == b.JobsExclusive
           && a.MinLevel == b.MinLevel && a.MaxLevel == b.MaxLevel && a.DyeableOnly == b.DyeableOnly && a.Wrists.SetEquals(b.Wrists)
           && a.Dye == b.Dye && a.Ownership == b.Ownership && a.WearableOnly == b.WearableOnly && a.Categories.SetEquals(b.Categories)
           && a.Expansions.SetEquals(b.Expansions);
}
