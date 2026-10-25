namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// The tile keys of the recently shown pages, the current one first, across slots and modes: the pages before the
/// current one stay rendered (see "Seiten im Cache"), so going back shows them at once.
/// </summary>
public sealed class PageHistory
{
    /// <summary>The most pages kept before the current one.</summary>
    public const int MaxPages = 20;

    private readonly List<IReadOnlyList<TileKey>> pages = new();

    /// <summary>
    /// Makes the page the current one; shown before, it moves to the front. An empty page (no results) is current
    /// too, so all others count as before it, but it is not kept once another page shows.
    /// </summary>
    public void Show(IReadOnlyList<TileKey> page)
    {
        if (pages.Count > 0 && pages[0].SequenceEqual(page))
            return;

        pages.RemoveAll(p => p.Count == 0 || p.SequenceEqual(page));
        pages.Insert(0, page);
        if (pages.Count > MaxPages + 1)
            pages.RemoveRange(MaxPages + 1, pages.Count - MaxPages - 1);
    }

    /// <summary>The keys of up to <paramref name="count"/> pages shown before the current one, most recent first.</summary>
    public IEnumerable<TileKey> Before(int count) => pages.Skip(1).Take(Math.Max(0, count)).SelectMany(p => p);

    public void Clear() => pages.Clear();
}
