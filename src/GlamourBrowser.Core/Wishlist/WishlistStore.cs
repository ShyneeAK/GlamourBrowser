namespace GlamourBrowser.Core.Wishlist;

/// <summary>
/// One character's wishlist: items and sets, in the order they were added. The favourites and the items clicked last
/// ("Zuletzt benutzt", see <see cref="Touch"/>) are kept in the same way.
/// </summary>
/// <param name="variants">"+N" variants mapped to their base item (see <see cref="Items.ItemVariants"/>): a wish for a variant is a wish for its base.</param>
public sealed class WishlistStore(ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
{
    private readonly List<WishEntry> entries = new();
    // The same keys as a set: the grid asks for every tile on every frame.
    private readonly HashSet<WishKey> keys = new();

    public ulong ContentId { get; } = contentId;

    public IReadOnlyList<WishEntry> Entries => entries;

    public event Action? Changed;

    public bool Contains(WishKey key) => keys.Contains(BaseOf(key));

    /// <summary>Returns false (and stays quiet) when the key, or the base item of a variant, is already listed.</summary>
    public bool Add(WishKey key, DateTimeOffset added)
    {
        if (Contains(key))
            return false;

        key = BaseOf(key);
        entries.Add(new WishEntry(key, added));
        keys.Add(key);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Returns false (and stays quiet) when the key was not listed.</summary>
    public bool Remove(WishKey key)
    {
        key = BaseOf(key);
        if (!keys.Remove(key))
            return false;

        entries.RemoveAll(e => e.Key == key);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Puts the key at the end (the newest), also when it is listed already, and drops the oldest entries beyond
    /// <paramref name="limit"/>.
    /// </summary>
    public void Touch(WishKey key, DateTimeOffset at, int limit)
    {
        key = BaseOf(key);
        if (entries.Count > 0 && entries[^1].Key == key)
            return;

        if (keys.Contains(key))
            entries.RemoveAll(e => e.Key == key);
        entries.Add(new WishEntry(key, at));
        keys.Add(key);
        if (entries.Count > limit)
        {
            foreach (var dropped in entries.Take(entries.Count - limit))
                keys.Remove(dropped.Key);
            entries.RemoveRange(0, entries.Count - limit);
        }
        Changed?.Invoke();
    }

    private WishKey BaseOf(WishKey key)
        => key.Kind == WishKind.Item && variants != null && variants.TryGetValue(key.ItemId, out var baseId) ? WishKey.ForItem(baseId) : key;
}
