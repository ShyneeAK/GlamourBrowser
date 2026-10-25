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
    private readonly SourceService sources;
    private Dictionary<SetId, SetSubject> byId = new();

    /// <remarks>Create it before <see cref="BrowserState"/>: both refresh on <see cref="SourceService.Ready"/>, the sets first.</remarks>
    public SetCatalog(IReadOnlyDictionary<uint, GlamourItem> items, SourceService sources)
    {
        this.items = items;
        this.sources = sources;
        sources.Ready += Resolve;
        if (sources.IsReady)
            Resolve();
    }

    public IReadOnlyList<SetSubject> All { get; private set; } = [];

    public SetSubject? Find(SetId id) => byId.GetValueOrDefault(id);

    private void Resolve()
    {
        All = sources.Index.Sets.Select(set => SetSubject.Resolve(set, items.GetValueOrDefault)).OfType<SetSubject>().ToList();
        byId = new Dictionary<SetId, SetSubject>();
        foreach (var set in All)
            byId.TryAdd(set.Set.Id, set);
    }
}
