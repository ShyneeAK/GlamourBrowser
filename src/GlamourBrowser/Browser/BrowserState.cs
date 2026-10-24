using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;

namespace GlamourBrowser.Browser;

/// <summary>Filter, paging and layout of the tile grid; keeps the render queue in sync with what is shown.</summary>
public sealed class BrowserState
{
    private readonly IReadOnlyList<GlamourItem> catalog;
    private readonly OwnershipService ownership;
    private readonly AppearanceTracker appearance;
    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly Configuration config;
    private readonly SourceService sources;
    private FilterState filter = new();

    public BrowserState(IReadOnlyList<GlamourItem> catalog, OwnershipService ownership, AppearanceTracker appearance,
                        TileQueue queue, TileCache<IDalamudTextureWrap> cache, RenderPool pool, Configuration config, SourceService sources)
    {
        this.sources = sources;
        this.catalog = catalog;
        this.ownership = ownership;
        this.appearance = appearance;
        this.queue = queue;
        this.cache = cache;
        this.config = config;
        // A config edited by hand (or saved by an older version) must not break loading.
        (config.Columns, config.Rows) = PageLayout.ClampGrid(config.Columns, config.Rows);

        appearance.Changed += InvalidateTiles;
        ownership.Changed += OnOwnershipChanged;
        pool.Disrupted += RequestTiles;
        sources.Ready += Refresh;
        Refresh();
    }

    public FilterState Filter
    {
        get => filter;
        set
        {
            filter = value;
            Refresh();
        }
    }

    public int PageIndex { get; private set; }
    public IReadOnlyList<GlamourItem> Results { get; private set; } = [];
    public IReadOnlyList<GlamourItem> CurrentPage { get; private set; } = [];
    public int PageSize => config.Columns * config.Rows;
    public int PageCount => ItemFilter.PageCount(Results.Count, PageSize);
    public PageLayoutResult Layout { get; private set; }
    public GlamourItem? Selected { get; set; }

    /// <summary>Items per source category under all other filters (the numbers behind the category checkboxes).</summary>
    public IReadOnlyDictionary<SourceCategory, int> CountsWithoutCategories { get; private set; } = new Dictionary<SourceCategory, int>();

    public void UpdateLayout(float width, float height, float spacing, float labelHeight)
    {
        var layout = PageLayout.Compute(config.Columns, config.Rows, width, height, spacing, labelHeight);
        var stepChanged = layout.RenderWidth != Layout.RenderWidth;
        Layout = layout;
        if (stepChanged)
            RequestTiles();
    }

    public void NextPage() => GoToPage(PageIndex + 1);

    public void PreviousPage() => GoToPage(PageIndex - 1);

    public void SetGrid(int columns, int rows)
    {
        (config.Columns, config.Rows) = PageLayout.ClampGrid(columns, rows);
        config.Save();
        Refresh();
    }

    public void SetTileMode(TileMode mode)
    {
        config.TileMode = mode;
        config.Save();
        RequestTiles();
    }

    public void SetSort(SortOrder sort)
    {
        config.Sort = sort;
        config.Save();
        Refresh();
    }

    public TileKey KeyFor(GlamourItem item)
        => new(item.Id, 0, 0, config.TileMode, appearance.Hash, Layout.RenderWidth == 0 ? PageLayout.RenderSteps[0] : Layout.RenderWidth);

    /// <summary>Filters again and re-requests the visible tiles.</summary>
    public void Refresh()
    {
        lock (RenderSync.Lock)
        {
            var store = ownership.Current;
            // Categories and patches only once the background index is ready; the sort always comes from the config.
            var index = sources.IsReady ? sources.Index : null;
            Results = ItemFilter.Apply(catalog, filter with { Sort = config.Sort }, id => store?.IsOwned(id) ?? false, appearance.Race, appearance.Sex,
                index == null ? null : index.CategoriesOf, index == null ? null : index.PatchOf);
            PageIndex = ItemFilter.ClampPage(PageIndex, Results.Count, PageSize);
            if (index != null)
            {
                var withoutCategories = ItemFilter.Apply(catalog, filter with { Categories = new HashSet<SourceCategory>() }, id => store?.IsOwned(id) ?? false,
                    appearance.Race, appearance.Sex, index.CategoriesOf, index.PatchOf);
                CountsWithoutCategories = index.Counts(withoutCategories.Select(i => i.Id));
            }
            RequestTiles();
        }
    }

    /// <summary>Drops all rendered tiles (look or camera changed) and renders the page again.</summary>
    public void InvalidateTiles()
    {
        lock (RenderSync.Lock)
        {
            cache.Clear();
            Refresh();
        }
    }

    private void GoToPage(int index)
    {
        lock (RenderSync.Lock)
        {
            PageIndex = ItemFilter.ClampPage(index, Results.Count, PageSize);
            RequestTiles();
        }
    }

    /// <summary>Queues the current page first, then the next one; the cache keeps previous, current and next.</summary>
    private void RequestTiles()
    {
        lock (RenderSync.Lock)
        {
            CurrentPage = ItemFilter.Page(Results, PageIndex, PageSize);
            if (!appearance.IsValid)
            {
                queue.Clear();
                return;
            }

            var current = CurrentPage.Select(KeyFor).ToList();
            var next = PageIndex + 1 < PageCount ? ItemFilter.Page(Results, PageIndex + 1, PageSize).Select(KeyFor).ToList() : [];
            var previous = PageIndex > 0 ? ItemFilter.Page(Results, PageIndex - 1, PageSize).Select(KeyFor).ToList() : [];

            cache.Retain(current.Concat(next).Concat(previous).ToHashSet());
            queue.SetRequests(current.Where(k => !cache.TryGet(k, out _)), next.Where(k => !cache.TryGet(k, out _)));
        }
    }

    private void OnOwnershipChanged()
    {
        if (filter.Ownership != OwnershipFilter.All)
            Refresh();
    }
}
