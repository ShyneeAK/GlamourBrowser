using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Wishlist;

public enum WishKind
{
    Item,
    Set,
}

/// <summary>What a wish is for: an item (<see cref="ItemId"/>) or a whole set (<see cref="Set"/>).</summary>
public readonly record struct WishKey(WishKind Kind, uint ItemId, SetId Set)
{
    public static WishKey ForItem(uint itemId) => new(WishKind.Item, itemId, default);

    public static WishKey ForSet(SetId set) => new(WishKind.Set, 0, set);

    public static WishKey Of(TileSubject subject) => subject switch
    {
        ItemSubject { Item: var item } => ForItem(item.Id),
        SetSubject { Set: var set } => ForSet(set.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };
}

public sealed record WishEntry(WishKey Key, DateTimeOffset Added);
