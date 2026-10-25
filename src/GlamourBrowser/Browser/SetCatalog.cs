using System.Collections.Generic;
using System.Linq;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Sources;

namespace GlamourBrowser.Browser;

/// <summary>The detected sets with their pieces from the item catalog; resolved once the source index is ready.</summary>
public sealed class SetCatalog
{
    private readonly IReadOnlyDictionary<uint, GlamourItem> items;
    private readonly IReadOnlyDictionary<uint, uint> variants;
    private readonly SourceService sources;
    private Dictionary<SetId, SetSubject> byId = new();

    /// <remarks>Create it before <see cref="BrowserState"/>: both refresh on <see cref="SourceService.Ready"/>, the sets first.</remarks>
    /// <param name="variants">"+N" variants mapped to their base item: a set piece that is a variant stands for its base.</param>
    public SetCatalog(IReadOnlyDictionary<uint, GlamourItem> items, IReadOnlyDictionary<uint, uint> variants, SourceService sources)
    {
        this.items = items;
        this.variants = variants;
        this.sources = sources;
        sources.Ready += Resolve;
        if (sources.IsReady)
            Resolve();
    }

    public IReadOnlyList<SetSubject> All { get; private set; } = [];

    public SetSubject? Find(SetId id) => byId.GetValueOrDefault(id);

    private void Resolve()
    {
        // "+1" series map onto their base series and drop out as duplicates.
        All = SetSubject.ResolveAll(sources.Index.Sets, id => items.GetValueOrDefault(variants.GetValueOrDefault(id, id)));
        byId = new Dictionary<SetId, SetSubject>();
        foreach (var set in All)
            byId.TryAdd(set.Set.Id, set);
    }
}
