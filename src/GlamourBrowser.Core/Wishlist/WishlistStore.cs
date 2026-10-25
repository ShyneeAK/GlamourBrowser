namespace GlamourBrowser.Core.Wishlist;

/// <summary>One character's wishlist: items and sets, in the order they were added.</summary>
/// <param name="variants">"+N" variants mapped to their base item (see <see cref="Items.ItemVariants"/>): a wish for a variant is a wish for its base.</param>
public sealed class WishlistStore(ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
{
    private readonly List<WishEntry> entries = new();

    public ulong ContentId { get; } = contentId;

    public IReadOnlyList<WishEntry> Entries => entries;

    public event Action? Changed;

    public bool Contains(WishKey key)
    {
        key = BaseOf(key);
        return entries.Exists(e => e.Key == key);
    }

    /// <summary>Returns false (and stays quiet) when the key, or the base item of a variant, is already listed.</summary>
    public bool Add(WishKey key, DateTimeOffset added)
    {
        if (Contains(key))
            return false;

        entries.Add(new WishEntry(BaseOf(key), added));
        Changed?.Invoke();
        return true;
    }

    /// <summary>Returns false (and stays quiet) when the key was not listed.</summary>
    public bool Remove(WishKey key)
    {
        key = BaseOf(key);
        if (entries.RemoveAll(e => e.Key == key) == 0)
            return false;

        Changed?.Invoke();
        return true;
    }

    private WishKey BaseOf(WishKey key)
        => key.Kind == WishKind.Item && variants != null && variants.TryGetValue(key.ItemId, out var baseId) ? WishKey.ForItem(baseId) : key;
}
