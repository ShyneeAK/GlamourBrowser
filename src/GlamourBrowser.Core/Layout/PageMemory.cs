using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Layout;

/// <summary>
/// The page of every view of the slot bar (a slot selection, the sets, the wishlist, everything), so that coming back
/// to a slot shows the page it was left on. Valid only while the other filters stay the same.
/// </summary>
public sealed class PageMemory
{
    private static readonly IReadOnlySet<EquipSlot> NoSlots = new HashSet<EquipSlot>();
    private readonly Dictionary<(string Slots, BrowseMode Mode), int> pages = new();

    /// <returns>
    /// The page to show after the filter changed: the remembered page of the new view (the first one for a new view);
    /// the current page when only the view stayed; the first page when another filter changed, which also forgets
    /// every remembered page (they belonged to another list).
    /// </returns>
    public int Switch(FilterState from, int page, FilterState to)
    {
        if (Rest(from) != Rest(to))
        {
            pages.Clear();
            return 0;
        }

        var (fromView, toView) = (View(from), View(to));
        if (fromView == toView)
            return page;
        pages[fromView] = page;
        return pages.GetValueOrDefault(toView);
    }

    /// <summary>For a new grid size or sort order: the remembered page numbers would point at other items.</summary>
    public void Forget() => pages.Clear();

    private static (string, BrowseMode) View(FilterState f) => (string.Join(",", f.Slots.Order()), f.Mode);

    // Everything but the view and the tile dye (it colours the tiles, the list stays); the other sets compare by
    // reference, so any change of them counts as a change.
    private static FilterState Rest(FilterState f) => f with { Slots = NoSlots, Mode = BrowseMode.Items, Dye = default };
}
