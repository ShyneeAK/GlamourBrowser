using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Sources;

/// <summary>Immutable item → sources lookup plus the detected sets, built once in the background and handed over as a whole.</summary>
public sealed class SourceIndex
{
    private static readonly IReadOnlySet<SourceCategory> OtherOnly = new HashSet<SourceCategory> { SourceCategory.Other };

    private readonly Dictionary<uint, IReadOnlyList<ItemSource>> sources;
    private readonly Dictionary<uint, IReadOnlySet<SourceCategory>> categories;
    private readonly Dictionary<uint, float> patches;

    private SourceIndex(Dictionary<uint, IReadOnlyList<ItemSource>> sources, Dictionary<uint, float> patches, IReadOnlyList<string> failures,
                        IReadOnlyList<GlamourSet> sets)
    {
        this.sources = sources;
        this.patches = patches;
        Failures = failures;
        Sets = sets;
        categories = sources.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<SourceCategory>)kv.Value.Select(s => s.Category).ToHashSet());
    }

    public static SourceIndex Empty { get; } = new Builder().Build();

    public IReadOnlyList<string> Failures { get; }

    public bool HasFailures => Failures.Count > 0;

    /// <summary>Dresser outfits, mog station sets and model series (see <see cref="SetDetection"/>).</summary>
    public IReadOnlyList<GlamourSet> Sets { get; }

    public IReadOnlyList<ItemSource> SourcesOf(uint itemId) => sources.TryGetValue(itemId, out var list) ? list : [];

    /// <summary>Items without any known source count as <see cref="SourceCategory.Other"/>.</summary>
    public IReadOnlySet<SourceCategory> CategoriesOf(uint itemId) => categories.TryGetValue(itemId, out var set) ? set : OtherOnly;

    /// <summary>Like <see cref="CategoriesOf"/>, but empty for items without any known source (for display, not filtering).</summary>
    public IReadOnlySet<SourceCategory> KnownCategoriesOf(uint itemId) => categories.TryGetValue(itemId, out var set) ? set : NoCategories;

    private static readonly IReadOnlySet<SourceCategory> NoCategories = new HashSet<SourceCategory>();

    /// <summary>Patch the item was added in, 0 when unknown.</summary>
    public float PatchOf(uint itemId) => patches.GetValueOrDefault(itemId);

    /// <summary>Number of items per category; each item counts at most once per category.</summary>
    public IReadOnlyDictionary<SourceCategory, int> Counts(IEnumerable<uint> itemIds)
    {
        var counts = new Dictionary<SourceCategory, int>();
        foreach (var id in itemIds)
        {
            foreach (var category in CategoriesOf(id))
                counts[category] = counts.GetValueOrDefault(category) + 1;
        }
        return counts;
    }

    public sealed class Builder
    {
        private readonly Dictionary<uint, List<ItemSource>> sources = new();
        private readonly Dictionary<uint, float> patches = new();
        private readonly List<string> failures = new();
        private IReadOnlyList<GlamourSet> sets = [];

        /// <summary>Adds a source; an identical category/title/detail for the same item is ignored.</summary>
        public void Add(uint itemId, ItemSource source)
        {
            if (!sources.TryGetValue(itemId, out var list))
                sources[itemId] = list = new List<ItemSource>();
            if (!list.Any(s => s.Category == source.Category && s.Title == source.Title && s.Detail == source.Detail))
                list.Add(source);
        }

        public void SetPatch(uint itemId, float patch) => patches[itemId] = patch;

        public void MarkFailure(string family) => failures.Add(family);

        public void SetSets(IReadOnlyList<GlamourSet> value) => sets = value.ToList();

        /// <summary>Gives every base item the sources of its "+N" variants (getting a variant also gets the look).</summary>
        public void MergeVariants(IReadOnlyDictionary<uint, uint> baseOf)
        {
            foreach (var (variant, baseId) in baseOf)
            {
                if (sources.TryGetValue(variant, out var list))
                {
                    foreach (var source in list.ToList())
                        Add(baseId, source);
                }
            }
        }

        /// <summary>Copies everything, so later changes to the builder do not affect the built index.</summary>
        public SourceIndex Build() => new(
            sources.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ItemSource>)kv.Value.ToList()),
            new Dictionary<uint, float>(patches),
            failures.ToList(),
            sets);
    }
}
