using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Ui;
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
    private readonly CharacterListService wishlist;
    // The list "Zuletzt benutzt" shows: while it is open, its own clicks do not reorder it (the tiles would jump away under
    // the mouse); another character's list (login) does.
    private WishlistStore? historyShown;
    private readonly CustomSetService customSets;
    // A job's base class, for the job filter (gear made for one job may list its base class too).
    private readonly Func<uint, uint> parentOf;
    // One subject per catalog item, made once instead of on every refresh.
    private readonly IReadOnlyDictionary<uint, ItemSubject> itemSubjects;
    // The rings once more as left rings, for "Ring links" in the slot bar (worn and framed on the left hand).
    private readonly IReadOnlyDictionary<uint, ItemSubject> leftRings;
    private FilterState filter = new();
    private DateTime filterSaved = DateTime.MinValue;
    private bool filterUnsaved;
    private readonly PageMemory pages = new();
    private readonly PageHistory history = new();
    private readonly TryOnService tryOn;
    // The arm each bracelet is on, for the side filter once every bracelet is read.
    private readonly WristSides wristSides;

    public BrowserState(IReadOnlyList<GlamourItem> catalog, OwnershipService ownership, AppearanceTracker appearance,
                        TileQueue queue, TileCache<IDalamudTextureWrap> cache, RenderPool pool, Configuration config, SourceService sources, SetCatalog sets,
                        CharacterListService wishlist, CharacterListService favorites, CharacterListService history, JobCatalog jobs,
                        CustomSetService customSets, TryOnService tryOn, WristSides wristSides)
    {
        Favorites = favorites;
        History = history;
        this.wristSides = wristSides;
        this.tryOn = tryOn;
        this.customSets = customSets;
        parentOf = jobs.ParentOf;
        this.sources = sources;
        this.sets = sets;
        this.wishlist = wishlist;
        this.catalog = catalog;
        itemSubjects = catalog.ToDictionary(i => i.Id, i => new ItemSubject(i));
        sameModel = SameModel.Groups(catalog);
        leftRings = catalog.Where(i => i.Slot == EquipSlot.RingRight).ToDictionary(i => i.Id, i => new ItemSubject(i with { Slot = EquipSlot.RingLeft }));
        this.ownership = ownership;
        this.appearance = appearance;
        this.queue = queue;
        this.cache = cache;
        this.config = config;
        // A config edited by hand (or saved by an older version) must not break loading.
        (config.Columns, config.Rows) = PageLayout.ClampGrid(config.Columns, config.Rows);
        // Where the browser was left; the search starts empty.
        filter = config.Filter?.ToState() ?? new FilterState();

        // Tiles take face and body from the look and their outfit from the try-on, which follows a changed outfit by
        // itself (TryOnService): new keys render, nothing needs to be thrown away. Race and gender may filter anew.
        appearance.Changed += Refresh;
        tryOn.TryOn.Changed += RequestTiles;
        // The pose is part of the tile key: new keys render, the cache drops the old ones; with the standard pose nothing changes.
        appearance.PoseChanged += RequestTiles;
        ownership.Changed += OnOwnershipChanged;
        wishlist.Changed += OnWishlistChanged;
        favorites.Changed += OnFavoritesChanged;
        history.Changed += OnHistoryChanged;
        customSets.Changed += OnCustomSetsChanged;
        pool.Disrupted += RequestTiles;
        sources.Ready += Refresh;
        wristSides.Ready += Refresh;
        Refresh();
    }

    /// <summary>The items marked with the heart of their tile ("Favoriten").</summary>
    public CharacterListService Favorites { get; }

    /// <summary>The items clicked last ("Zuletzt benutzt"), the newest first.</summary>
    public CharacterListService History { get; }

    public FilterState Filter
    {
        get => filter;
        set
        {
            // Another filter closes the pieces of an opened set (or the items of one look), back on the page they were opened
            // from; the tile dye dyes them anew.
            if (PiecesOpen && value with { Dye = filter.Dye } != filter)
            {
                (OpenSets, OpenModel) = (null, null);
                PageIndex = pageBeforeSets;
            }
            // Back on a slot (or mode) the page it was left on; Refresh clamps it to the new results.
            PageIndex = pages.Switch(filter, PageIndex, value);
            // A new tile dye dyes every tile, the single ones too.
            if (value.Dye != filter.Dye)
                tileDyes.Clear();
            // Another mode closes an open folder of "Meine Sets".
            if (value.Mode != BrowseMode.MySets)
                OpenFolder = 0;
            // Kept for the next start; the search is not, so typing does not write the configuration. Written at most once
            // a second (the level slider changes the filter on every frame of a drag), the last change by SaveFilterIfDue.
            if (!SavedFilter.SameKept(filter, value))
            {
                config.Filter = SavedFilter.From(value);
                filterUnsaved = true;
                SaveFilterIfDue();
            }
            filter = value;
            Refresh();
        }
    }

    public int PageIndex { get; private set; }
    // The last page change went towards the first page: the pages before render ahead.
    private bool pagingBack;
    public IReadOnlyList<TileSubject> Results { get; private set; } = [];
    public IReadOnlyList<TileSubject> CurrentPage { get; private set; } = [];
    public int PageSize => config.Columns * config.Rows;
    public int PageCount => ItemFilter.PageCount(Results.Count, PageSize);
    public PageLayoutResult Layout { get; private set; }
    public TileSubject? Selected { get; set; }

    /// <summary>The folder of "Meine Sets" that is open; 0 for the top with the folders.</summary>
    public uint OpenFolder { get; private set; }
    // The page of the top level a folder was opened from, to go back to it.
    private int pageBeforeFolder;

    /// <summary>Opens a folder of "Meine Sets" on its first page.</summary>
    public void ShowFolder(uint folder)
    {
        lock (RenderSync.Lock)
        {
            if (OpenFolder == 0)
                pageBeforeFolder = PageIndex;
            OpenFolder = folder;
            PageIndex = 0;
            Refresh();
        }
    }

    /// <summary>Back to the top of "Meine Sets", on the page the folder was opened from.</summary>
    public void CloseFolder()
    {
        lock (RenderSync.Lock)
        {
            if (OpenFolder == 0)
                return;
            OpenFolder = 0;
            PageIndex = pageBeforeFolder;
            Refresh();
        }
    }

    /// <summary>
    /// The sets whose pieces the grid shows as item tiles (opened with the chest in the item card), instead of what the
    /// filter finds; null while closed.
    /// </summary>
    public IReadOnlyList<SetSubject>? OpenSets { get; private set; }

    /// <summary>
    /// The item whose look the grid shows: every item with the same models (see <see cref="SameModelAs"/>), opened from a
    /// tile, instead of what the filter finds; null while closed.
    /// </summary>
    public GlamourItem? OpenModel { get; private set; }

    /// <summary>The grid shows the pieces of sets or the items of one look instead of what the filter finds.</summary>
    public bool PiecesOpen => OpenSets != null || OpenModel != null;

    // The page the pieces were opened from, to go back to it.
    private int pageBeforeSets;
    // Every item that shares its look with others, with all items of that look.
    private readonly IReadOnlyDictionary<uint, IReadOnlyList<GlamourItem>> sameModel;

    /// <summary>The items that look like this one (itself included), lowest level first; empty when no other item does.</summary>
    public IReadOnlyList<GlamourItem> SameModelAs(uint itemId) => sameModel.GetValueOrDefault(itemId) ?? [];

    /// <summary>Shows the pieces of the sets as tiles, on their first page; the filter stays for going back.</summary>
    public void ShowSetPieces(IReadOnlyList<SetSubject> sets) => Open(sets, null);

    /// <summary>Shows every item that looks like this one as tiles, on their first page; the filter stays for going back.</summary>
    public void ShowSameModel(GlamourItem item) => Open(null, item);

    private void Open(IReadOnlyList<SetSubject>? sets, GlamourItem? model)
    {
        lock (RenderSync.Lock)
        {
            if (!PiecesOpen)
                pageBeforeSets = PageIndex;
            (OpenSets, OpenModel) = (sets, model);
            PageIndex = 0;
            Refresh();
        }
    }

    /// <summary>Back to what the filter finds, on the page the pieces were opened from.</summary>
    public void CloseSetPieces()
    {
        lock (RenderSync.Lock)
        {
            if (!PiecesOpen)
                return;
            (OpenSets, OpenModel) = (null, null);
            PageIndex = pageBeforeSets;
            Refresh();
        }
    }

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

    public void ShowLastPage() => GoToPage(PageCount - 1);

    /// <summary>Writes a changed filter once the last write is a second old; called on every framework tick too.</summary>
    public void SaveFilterIfDue()
    {
        if (!filterUnsaved || DateTime.UtcNow - filterSaved < TimeSpan.FromSeconds(1))
            return;
        config.Save();
        filterSaved = DateTime.UtcNow;
        filterUnsaved = false;
    }

    /// <summary>Pages forward (or back, negative) by <paramref name="pages"/>, stopping at the first and the last page.</summary>
    public void MoveBy(int pages) => GoToPage(PageIndex + pages);

    /// <summary>The page with this index (0 for the first), clamped to the pages there are.</summary>
    public void ShowPage(int index) => GoToPage(index);

    /// <summary>
    /// The item's slot in the item mode, on the page with its tile, to browse on from there. The other filters stay;
    /// when they hide the item, the slot's first page and false.
    /// </summary>
    public bool ShowInGrid(GlamourItem item)
    {
        lock (RenderSync.Lock)
        {
            Filter = filter with { Mode = BrowseMode.Items, Slots = new HashSet<EquipSlot> { item.Slot } };
            var index = IndexOf(item.Id);
            GoToPage(Math.Max(0, index) / PageSize);
            return index >= 0;
        }

        int IndexOf(uint id)
        {
            for (var i = 0; i < Results.Count; i++)
            {
                if (Results[i] is ItemSubject { Item.Id: var found } && found == id)
                    return i;
            }
            return -1;
        }
    }

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
        if (config.TileMode == mode)
            return;
        config.TileMode = mode;
        config.Save();
        // Like the game's "Alles anlegen / ablegen": the clicked pieces stay, over the new basis.
        tryOn.Rebase();
    }

    /// <summary>The tile dye where it applies: not in the wishlist and custom set modes, which only search.</summary>
    public DyePair TileDye => BrowseModes.OnlySearch(filter.Mode) ? default : filter.Dye;

    // Dyes chosen on single item tiles; a new tile dye replaces them.
    private readonly TileDyeOverrides tileDyes = new();

    /// <summary>The dye a tile shows and puts on: an item tile's own (its dye boxes), else the tile dye.</summary>
    public DyePair DyeFor(TileSubject subject) => subject is ItemSubject { Item: var item } ? tileDyes.For(item.Id, TileDye) : TileDye;

    /// <summary>Whether the item tile has a dye of its own, not the tile dye.</summary>
    public bool HasOwnDye(GlamourItem item) => tileDyes.Has(item.Id);

    /// <summary>Dyes one item tile (and only it); the tile dye again drops its own.</summary>
    public void SetDye(GlamourItem item, DyePair dye)
    {
        lock (RenderSync.Lock)
        {
            tileDyes.Set(item.Id, dye, TileDye);
            RequestTiles();
        }
    }

    /// <summary>The look part of a tile key: face and body, and the try-on without the slots the tile covers (see <see cref="TryOn.LookFor"/>).</summary>
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
        SetSubject set => TileKeys.ForSet(set.Set.Id, config.TileMode, LookHash(CoverOf(set)), RenderWidth, IdleStance.Pose, set.Variant,
                                          set.Set.Id.Kind == SetKind.Custom ? default : TileDye),
        ItemSubject { Item: var item } => ItemKey(item, CoverOf(subject)),
        FolderSubject { Cover: { } cover } => KeyFor(cover),
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    /// <summary>An item tile: the item in the tile dye on its channels, its stance, on the try-on without the slots it covers.</summary>
    private TileKey ItemKey(GlamourItem item, ushort cover)
    {
        var (stain0, stain1) = TileDyes.For(item, tileDyes.For(item.Id, TileDye));
        var stance = StanceFor(item);
        return new(item.Id, stain0, stain1, config.TileMode, LookHash(cover), RenderWidth,
                   stance.WeaponDrawn ? TileKey.StandardPose : stance.Pose, stance.WeaponDrawn, LeftHand: item.Slot == EquipSlot.RingLeft);
    }

    /// <summary>The catalog item's subject, the same instance the grid shows (null for an item outside the catalog).</summary>
    public ItemSubject? ItemSubjectFor(uint itemId) => itemSubjects.GetValueOrDefault(itemId);

    /// <summary>The item's subject as worn in <paramref name="slot"/>: a ring on the left hand is the left ring's tile.</summary>
    public ItemSubject? ItemSubjectFor(uint itemId, EquipSlot slot)
        => slot == EquipSlot.RingLeft && leftRings.TryGetValue(itemId, out var left) ? left : itemSubjects.GetValueOrDefault(itemId);

    /// <summary>The item or set a tile key was made for (the render pool's lookup).</summary>
    public TileSubject? SubjectFor(TileKey key)
        => key.Set.IsSet ? SetSubjectFor(key.Set) : ItemSubjectFor(key.ItemId, key.LeftHand ? EquipSlot.RingLeft : EquipSlot.RingRight);

    /// <summary>A detected set or one of the custom sets.</summary>
    public SetSubject? SetSubjectFor(SetId id) => id.Kind == SetKind.Custom ? MySets.FirstOrDefault(s => s.Set.Id == id) : sets.Find(id);

    /// <summary>The detected sets the catalog item is a piece of (none until the source index is ready).</summary>
    public IReadOnlyList<SetSubject> SetsOf(uint itemId) => sets.SetsOf(itemId);

    /// <summary>The catalog item for a custom set piece (null when the catalog does not know it).</summary>
    public GlamourItem? ItemOf(uint itemId) => itemSubjects.TryGetValue(itemId, out var subject) ? subject.Item : null;

    private Func<uint, WristSide>? WristOf => wristSides.IsReady ? wristSides.Of : null;

    private int RenderWidth => Layout.RenderWidth == 0 ? PageLayout.RenderSteps[0] : Layout.RenderWidth;

    public static readonly Stance WeaponDrawnStance = new(true, 0);

    /// <summary>How the character stands on the item's tile: weapons drawn (when enabled), otherwise the idle pose.</summary>
    /// <summary>The live view hides the weapon: tiles that do not show a weapon themselves leave the try-on's out.</summary>
    public bool WeaponsHidden { get; private set; }

    public void SetWeaponsHidden(bool hidden)
    {
        lock (RenderSync.Lock)
        {
            if (WeaponsHidden == hidden)
                return;
            WeaponsHidden = hidden;
            RequestTiles();
        }
    }

    // Every slot: a wishlist or "Meine Sets" tile wears nothing of the try-on.
    private static readonly ushort AllSlots = SlotMask.Of(CustomSetSubjects.Slots);

    /// <summary>
    /// The slots a tile leaves free of the try-on around it (<see cref="LiveWeapons.TileCover(TileSubject, bool)"/>); in
    /// the wishlist and in "Meine Sets" all of them, so a wish or an own set shows only what it holds (no weapon of the
    /// outfit either), like with everything off.
    /// </summary>
    public ushort CoverOf(TileSubject subject)
        => filter.Mode is BrowseMode.Wishlist or BrowseMode.MySets && !PiecesOpen ? AllSlots : LiveWeapons.TileCover(subject, WeaponsHidden);

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
            if (OpenSets is { } open)
                Results = open.SelectMany(set => set.Pieces).DistinctBy(piece => piece.Id)
                              .Select(piece => ItemSubjectFor(piece.Id)).OfType<TileSubject>().ToList();
            // A left ring opened stays on the left hand.
            else if (OpenModel is { } model)
                Results = SameModelAs(model.Id).Select(item => ItemSubjectFor(item.Id, model.Slot)).OfType<TileSubject>().ToList();
            else if (filter.Mode == BrowseMode.MySets)
            {
                var folders = customSets.Current?.Folders ?? [];
                // A deleted folder (or another character's) closes.
                if (OpenFolder != 0 && folders.All(f => f.Id != OpenFolder))
                    OpenFolder = 0;
                var found = WishResults.Filter(MySets, filter.SearchText, hideDone: false, isOwned).OfType<SetSubject>().ToList();
                Results = CustomSetFolders.View(found, folders, id => customSets.Current?.Find(id)?.Folder ?? 0, OpenFolder,
                                                filter.SearchText.Trim().Length > 0);
            }
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
            else if (BrowseModes.ItemList(filter.Mode))
            {
                var list = filter.Mode == BrowseMode.History ? History : Favorites;
                if (filter.Mode == BrowseMode.History)
                    historyShown = History.Current;
                ShowItems(ListedItems(list), keepOrder: true);
            }
            else
                ShowItems(catalog, keepOrder: false);
            PageIndex = ItemFilter.ClampPage(PageIndex, Results.Count, PageSize);
            RequestTiles();

            void ShowItems(IEnumerable<GlamourItem> items, bool keepOrder)
            {
                if (index == null)
                {
                    Results = Subjects(ItemFilter.Apply(items, sorted, isOwned, appearance.Race, appearance.Sex, parentOf: parentOf, wristOf: WristOf,
                                                        keepOrder: keepOrder));
                    return;
                }
                // One pass for both: the numbers behind the category checkboxes ignore the categories themselves.
                var (shown, withoutCategories) = ItemFilter.ApplyWithCategoryBase(items, sorted, isOwned,
                    appearance.Race, appearance.Sex, index.CategoriesOf, index.PatchOf, parentOf, wristOf: WristOf, keepOrder: keepOrder);
                Results = Subjects(shown);
                // With the duplicates hidden a model counts once, like its single tile.
                CountsWithoutCategories = filter.HideDuplicates
                    ? SameModel.Counts(withoutCategories, index.CategoriesOf)
                    : index.Counts(withoutCategories.Select(i => i.Id));
            }
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
            var page = ItemFilter.ClampPage(index, Results.Count, PageSize);
            if (page != PageIndex)
                pagingBack = page < PageIndex;
            PageIndex = page;
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
            if (!appearance.IsValid || filter.Mode == BrowseMode.Wishlist && config.WishlistBySource && !PiecesOpen)
            {
                queue.Clear();
                return;
            }

            var current = RenderKeys(CurrentPage).ToList();
            // The pages ahead in the paging direction render the nearest first; recently shown pages (also of other slots)
            // stay cached for going back.
            var lookahead = Lookahead.Pages(PageIndex, PageCount, config.LookaheadPages, pagingBack).SelectMany(PageKeys).ToList();
            history.Show(current);

            cache.Retain(current.Concat(lookahead).Concat(history.Before(config.CachedPages)).ToHashSet());
            queue.SetRequests(current.Where(k => !cache.TryGet(k, out _)), lookahead.Where(k => !cache.TryGet(k, out _)));
        }
    }

    private List<TileKey> PageKeys(int page)
        => page >= 0 && page < PageCount ? RenderKeys(ItemFilter.Page(Results, page, PageSize)).ToList() : [];

    /// <summary>The keys to render for the subjects: a folder renders its first sets (its collage), an empty one nothing.</summary>
    private IEnumerable<TileKey> RenderKeys(IEnumerable<TileSubject> subjects)
        => subjects.SelectMany(s => s is FolderSubject folder ? folder.Covers : (IEnumerable<TileSubject>)[s]).Select(KeyFor);

    /// <summary>The items' tiles; rings on the hand the slot bar chose.</summary>
    private IReadOnlyList<TileSubject> Subjects(IReadOnlyList<GlamourItem> items)
    {
        var hand = SlotSelection.RingHand(filter.Slots);
        return items.Select(i => (TileSubject)ItemSubjectFor(i.Id, hand)!).ToList();
    }

    private IReadOnlyList<TileSubject> ResolveWishlist()
        => wishlist.Current is { } store
            ? WishResults.Resolve(store.Entries, id => itemSubjects.GetValueOrDefault(id), SetSubjectFor)
            : [];

    /// <summary>The items of a list (favourites, history) the catalog knows, the newest first.</summary>
    private IEnumerable<GlamourItem> ListedItems(CharacterListService list)
        => (list.Current?.Entries ?? []).Reverse()
            .Where(e => e.Key.Kind == WishKind.Item)
            .Select(e => itemSubjects.GetValueOrDefault(e.Key.ItemId)?.Item)
            .OfType<GlamourItem>();

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

    private void OnFavoritesChanged()
    {
        if (filter.Mode == BrowseMode.Favorites)
            Refresh();
    }

    private void OnHistoryChanged()
    {
        if (filter.Mode == BrowseMode.History && History.Current != historyShown)
            Refresh();
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
