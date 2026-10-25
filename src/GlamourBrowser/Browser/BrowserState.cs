using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.CustomSets;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.Wishlist;

namespace GlamourBrowser.Browser;

/// <summary>Filter, paging and layout of the tile grid (items, sets or the wishlist); keeps the render queue in sync with what is shown.</summary>
public sealed class BrowserState
{
    private readonly IReadOnlyList<GlamourItem> catalog;
    private readonly OwnershipService ownership;
    private readonly AppearanceTracker appearance;
    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly Configuration config;
    private readonly SourceService sources;
    private readonly SetCatalog sets;
    private readonly WishlistService wishlist;
    private readonly CustomSetService customSets;
    // A job's base class, for the job filter (gear made for one job may list its base class too).
    private readonly Func<uint, uint> parentOf;
    // One subject per catalog item, made once instead of on every refresh.
    private readonly IReadOnlyDictionary<uint, ItemSubject> itemSubjects;
    private FilterState filter = new();
    private readonly PageMemory pages = new();
    private readonly PageHistory history = new();
    private readonly TryOnService tryOn;

    public BrowserState(IReadOnlyList<GlamourItem> catalog, OwnershipService ownership, AppearanceTracker appearance,
                        TileQueue queue, TileCache<IDalamudTextureWrap> cache, RenderPool pool, Configuration config, SourceService sources, SetCatalog sets,
                        WishlistService wishlist, JobCatalog jobs, CustomSetService customSets, TryOnService tryOn)
    {
        this.tryOn = tryOn;
        this.customSets = customSets;
        parentOf = jobs.ParentOf;
        this.sources = sources;
        this.sets = sets;
        this.wishlist = wishlist;
        this.catalog = catalog;
        itemSubjects = catalog.ToDictionary(i => i.Id, i => new ItemSubject(i));
        this.ownership = ownership;
        this.appearance = appearance;
        this.queue = queue;
        this.cache = cache;
        this.config = config;
        // A config edited by hand (or saved by an older version) must not break loading.
        (config.Columns, config.Rows) = PageLayout.ClampGrid(config.Columns, config.Rows);

        // Tiles take face and body from the look and their outfit from the try-on, which follows a changed outfit by
        // itself (TryOnService): new keys render, nothing needs to be thrown away. Race and gender may filter anew.
        appearance.Changed += Refresh;
        tryOn.TryOn.Changed += RequestTiles;
        // The pose is part of the tile key: new keys render, the cache drops the old ones; with the standard pose nothing changes.
        appearance.PoseChanged += RequestTiles;
        ownership.Changed += OnOwnershipChanged;
        wishlist.Changed += OnWishlistChanged;
        customSets.Changed += OnCustomSetsChanged;
        pool.Disrupted += RequestTiles;
        sources.Ready += Refresh;
        Refresh();
    }

    public FilterState Filter
    {
        get => filter;
        set
        {
            // Back on a slot (or mode) the page it was left on; Refresh clamps it to the new results.
            PageIndex = pages.Switch(filter, PageIndex, value);
            filter = value;
            Refresh();
        }
    }

    public int PageIndex { get; private set; }
    public IReadOnlyList<TileSubject> Results { get; private set; } = [];
    public IReadOnlyList<TileSubject> CurrentPage { get; private set; } = [];
    public int PageSize => config.Columns * config.Rows;
    public int PageCount => ItemFilter.PageCount(Results.Count, PageSize);
    public PageLayoutResult Layout { get; private set; }
    public TileSubject? Selected { get; set; }

    /// <summary>The custom sets of the logged-in character that have a known piece, the last changed first.</summary>
    public IReadOnlyList<SetSubject> MySets { get; private set; } = [];

    /// <summary>Every wishlist entry that resolves to an item or set, newest first (sets only once the source index is ready).</summary>
    public IReadOnlyList<TileSubject> WishlistEntries { get; private set; } = [];

    /// <summary>Items (or sets) per source category under all other filters (the numbers behind the category checkboxes).</summary>
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

    public void ShowFirstPage() => GoToPage(0);

    public void SetGrid(int columns, int rows)
    {
        // The page's first item stays in view; remembered pages of other slots no longer match the page size.
        var first = PageIndex * PageSize;
        (config.Columns, config.Rows) = PageLayout.ClampGrid(columns, rows);
        config.Save();
        pages.Forget();
        PageIndex = first / PageSize;
        Refresh();
    }

    /// <summary>The tile mode is the try-on's basis: switching it starts the try-on afresh (which renders the tiles anew).</summary>
    public void SetTileMode(TileMode mode)
    {
        // Picking the mode it already is (a combo reports that as a change too) must not throw the try-on away.
        if (config.TileMode == mode)
            return;
        config.TileMode = mode;
        config.Save();
        tryOn.Reset();
    }

    /// <summary>The tile dye where it applies: not in the wishlist and custom set modes, which only search.</summary>
    public DyePair TileDye => BrowseModes.OnlySearch(filter.Mode) ? default : filter.Dye;

    /// <summary>The look part of a tile key: face and body, and the try-on without the slots the tile replaces.</summary>
    private ulong LookHash(ushort exceptSlots) => appearance.CustomizeHash * 31 + tryOn.TryOn.KeyHash(exceptSlots);

    public void SetTileWeaponsDrawn(bool drawn)
    {
        config.TileWeaponsDrawn = drawn;
        config.Save();
        RequestTiles();
    }

    /// <summary>Fewer pages drop their tiles right away; more keep the ones shown from now on.</summary>
    public void SetCachedPages(int count)
    {
        config.CachedPages = Math.Clamp(count, 0, PageHistory.MaxPages);
        config.Save();
        RequestTiles();
    }

    public void SetLookaheadPages(int count)
    {
        config.LookaheadPages = Math.Clamp(count, 0, Lookahead.MaxPages);
        config.Save();
        RequestTiles();
    }

    public void SetUseIdlePose(bool useIdlePose)
    {
        config.UseIdlePose = useIdlePose;
        config.Save();
        RequestTiles();
    }

    public void SetHideDoneWishes(bool hide)
    {
        config.HideDoneWishes = hide;
        config.Save();
        Refresh();
    }

    public void SetWishlistBySource(bool bySource)
    {
        config.WishlistBySource = bySource;
        config.Save();
        Refresh();
    }

    public void SetSort(SortOrder sort)
    {
        // Another order is another list: from its start, and remembered pages no longer apply.
        config.Sort = sort;
        config.Save();
        pages.Forget();
        PageIndex = 0;
        Refresh();
    }

    public TileKey KeyFor(TileSubject subject) => subject switch
    {
        SetSubject set => TileKeys.ForSet(set.Set.Id, config.TileMode, LookHash(set.Slots), RenderWidth, IdleStance.Pose, set.Variant,
                                          set.Set.Id.Kind == SetKind.Custom ? default : TileDye),
        ItemSubject { Item: var item } => ItemKey(item),
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    /// <summary>An item tile: the item in the tile dye on its channels, its stance, on the try-on without its slot.</summary>
    private TileKey ItemKey(GlamourItem item)
    {
        var (stain0, stain1) = TileDyes.For(item, TileDye);
        var stance = StanceFor(item);
        return new(item.Id, stain0, stain1, config.TileMode, LookHash(SlotMask.Of([item.Slot])), RenderWidth,
                   stance.WeaponDrawn ? TileKey.StandardPose : stance.Pose, stance.WeaponDrawn);
    }

    /// <summary>The catalog item's subject, the same instance the grid shows (null for an item outside the catalog).</summary>
    public ItemSubject? ItemSubjectFor(uint itemId) => itemSubjects.GetValueOrDefault(itemId);

    /// <summary>The item or set a tile key was made for (the render pool's lookup).</summary>
    public TileSubject? SubjectFor(TileKey key) => key.Set.IsSet ? SetSubjectFor(key.Set) : itemSubjects.GetValueOrDefault(key.ItemId);

    /// <summary>A detected set or one of the custom sets.</summary>
    public SetSubject? SetSubjectFor(SetId id) => id.Kind == SetKind.Custom ? MySets.FirstOrDefault(s => s.Set.Id == id) : sets.Find(id);

    /// <summary>The catalog item for a custom set piece (null when the catalog does not know it).</summary>
    public GlamourItem? ItemOf(uint itemId) => itemSubjects.TryGetValue(itemId, out var subject) ? subject.Item : null;

    private int RenderWidth => Layout.RenderWidth == 0 ? PageLayout.RenderSteps[0] : Layout.RenderWidth;

    public static readonly Stance WeaponDrawnStance = new(true, 0);

    /// <summary>How the character stands on the item's tile: weapons drawn (when enabled), otherwise the idle pose.</summary>
    public Stance StanceFor(GlamourItem item)
        => config.TileWeaponsDrawn && item.Slot is EquipSlot.MainHand or EquipSlot.OffHand
            ? WeaponDrawnStance
            : IdleStance;

    /// <summary>Standing without a drawn weapon: the player's idle pose, or the standard pose when that is turned off.</summary>
    public Stance IdleStance => new(false, config.UseIdlePose ? appearance.IdlePose : TileKey.StandardPose);

    /// <summary>Filters again and re-requests the visible tiles.</summary>
    public void Refresh()
    {
        lock (RenderSync.Lock)
        {
            var store = ownership.Current;
            // Categories and patches only once the background index is ready; the sort always comes from the config.
            var index = sources.IsReady ? sources.Index : null;
            Func<uint, bool> isOwned = id => store?.IsOwned(id) ?? false;
            var sorted = filter with { Sort = config.Sort };
            MySets = ResolveMySets();
            WishlistEntries = ResolveWishlist();
            if (filter.Mode == BrowseMode.MySets)
                Results = WishResults.Filter(MySets, filter.SearchText, hideDone: false, isOwned);
            else if (filter.Mode == BrowseMode.Wishlist)
                Results = WishResults.Filter(WishlistEntries, filter.SearchText, config.HideDoneWishes, isOwned);
            else if (filter.Mode == BrowseMode.Sets)
            {
                // Without the index there are no sets yet (the grid says so).
                var (setResults, withoutCategories) = SetFilter.Apply(index == null ? [] : sets.All, sorted, isOwned, appearance.Race, appearance.Sex,
                    index == null ? null : index.CategoriesOf, index == null ? null : index.PatchOf, parentOf);
                Results = setResults;
                if (index != null)
                    CountsWithoutCategories = SetFilter.Counts(withoutCategories, index.CategoriesOf);
            }
            else if (index == null)
                Results = Subjects(ItemFilter.Apply(catalog, sorted, isOwned, appearance.Race, appearance.Sex, parentOf: parentOf));
            else
            {
                // One pass for both: the numbers behind the category checkboxes ignore the categories themselves.
                var (items, withoutCategories) = ItemFilter.ApplyWithCategoryBase(catalog, sorted, isOwned,
                    appearance.Race, appearance.Sex, index.CategoriesOf, index.PatchOf, parentOf);
                Results = Subjects(items);
                CountsWithoutCategories = index.Counts(withoutCategories.Select(i => i.Id));
            }
            PageIndex = ItemFilter.ClampPage(PageIndex, Results.Count, PageSize);
            RequestTiles();
        }
    }

    /// <summary>Drops all rendered tiles (look or camera changed) and renders the page again.</summary>
    public void InvalidateTiles()
    {
        lock (RenderSync.Lock)
        {
            cache.Clear();
            history.Clear();
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

    /// <summary>Queues the current page first, then the ones ahead; the cache keeps these and the recently shown pages.</summary>
    private void RequestTiles()
    {
        lock (RenderSync.Lock)
        {
            CurrentPage = ItemFilter.Page(Results, PageIndex, PageSize);
            // The plan by source shows no tiles: nothing to render, the cached ones stay for switching back.
            if (!appearance.IsValid || filter.Mode == BrowseMode.Wishlist && config.WishlistBySource)
            {
                queue.Clear();
                return;
            }

            var current = CurrentPage.Select(KeyFor).ToList();
            // The pages ahead render the next one first; recently shown pages (also of other slots) stay cached for going back.
            var lookahead = Lookahead.Pages(PageIndex, PageCount, config.LookaheadPages).SelectMany(PageKeys).ToList();
            history.Show(current);

            cache.Retain(current.Concat(lookahead).Concat(history.Before(config.CachedPages)).ToHashSet());
            queue.SetRequests(current.Where(k => !cache.TryGet(k, out _)), lookahead.Where(k => !cache.TryGet(k, out _)));
        }
    }

    private List<TileKey> PageKeys(int page)
        => page >= 0 && page < PageCount ? ItemFilter.Page(Results, page, PageSize).Select(KeyFor).ToList() : [];

    private IReadOnlyList<TileSubject> Subjects(IReadOnlyList<GlamourItem> items) => items.Select(i => (TileSubject)itemSubjects[i.Id]).ToList();

    private IReadOnlyList<TileSubject> ResolveWishlist()
        => wishlist.Current is { } store
            ? WishResults.Resolve(store.Entries, id => itemSubjects.GetValueOrDefault(id), SetSubjectFor)
            : [];

    private IReadOnlyList<SetSubject> ResolveMySets()
        => customSets.Current?.Sets.Select(set => CustomSetSubjects.ToSubject(set, ItemOf)).OfType<SetSubject>().ToList() ?? [];

    private void OnOwnershipChanged()
    {
        if (filter.Ownership != OwnershipFilter.All || filter.Mode is BrowseMode.Wishlist or BrowseMode.MySets)
            Refresh();
    }

    private void OnCustomSetsChanged()
    {
        // A custom set on the wishlist changes the wishlist too.
        if (filter.Mode is BrowseMode.MySets or BrowseMode.Wishlist)
            Refresh();
        else
        {
            lock (RenderSync.Lock)
            {
                MySets = ResolveMySets();
                WishlistEntries = ResolveWishlist();
            }
        }
    }

    private void OnWishlistChanged()
    {
        if (filter.Mode == BrowseMode.Wishlist)
            Refresh();
        else
        {
            lock (RenderSync.Lock)
                WishlistEntries = ResolveWishlist();
        }
    }
}
