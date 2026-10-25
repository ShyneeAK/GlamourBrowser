namespace GlamourBrowser.Core.Wishlist;

/// <summary>One character's wishlist: items and sets, in the order they were added.</summary>
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

    private WishKey BaseOf(WishKey key)
        => key.Kind == WishKind.Item && variants != null && variants.TryGetValue(key.ItemId, out var baseId) ? WishKey.ForItem(baseId) : key;
}
