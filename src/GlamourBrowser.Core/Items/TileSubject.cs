using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Items;

/// <summary>What a tile, the live preview and the item card show: a single item or a whole set.</summary>
public abstract record TileSubject
{
    /// <summary>Identity for selection and caches: the item id for items, the set id for sets.</summary>
    public abstract (uint ItemId, SetId Set) Id { get; }

    public abstract string Name { get; }

    /// <summary>The items the character puts on, at most one per slot.</summary>
    public abstract IReadOnlyList<GlamourItem> Wear { get; }

    /// <summary>The slot whose camera calibration frames the subject.</summary>
    public abstract EquipSlot CameraSlot { get; }

    public ushort Slots => SlotMask.Of(Wear.Select(w => w.Slot));
}

public sealed record ItemSubject(GlamourItem Item) : TileSubject
{
    public override (uint ItemId, SetId Set) Id => (Item.Id, default);
    public override string Name => Item.Name;
    public override IReadOnlyList<GlamourItem> Wear => [Item];
    public override EquipSlot CameraSlot => Item.Slot;
}

/// <param name="Pieces">The set's pieces found in the item catalog, at most one per slot.</param>
public sealed record SetSubject(GlamourSet Set, IReadOnlyList<GlamourItem> Pieces) : TileSubject
{
    private static readonly EquipSlot[] Armour = [EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet];

    public override (uint ItemId, SetId Set) Id => (0, Set.Id);
    public override string Name => Set.Name;
    public override IReadOnlyList<GlamourItem> Wear => Pieces;
    public override EquipSlot CameraSlot => EquipSlot.Body;

    /// <summary>Jobs that can wear every armour piece (every piece when the set has no armour).</summary>
    public IReadOnlySet<uint> ArmourJobs { get; } = CommonJobs(Pieces);

    public bool Allows(byte race, byte sex) => Pieces.All(p => p.Restriction.Allows(race, sex));

    /// <summary>The set with its pieces from the catalog; unknown pieces drop out, the first piece of a slot wins. Null below 2 pieces.</summary>
    public static SetSubject? Resolve(GlamourSet set, Func<uint, GlamourItem?> itemOf)
    {
        var pieces = set.Pieces.Select(p => itemOf(p.ItemId)).OfType<GlamourItem>().GroupBy(i => i.Slot).Select(slot => slot.First()).ToList();
        return pieces.Count < 2 ? null : new SetSubject(set, pieces);
    }

    /// <summary>
    /// Resolves every set (see <see cref="Resolve"/>) and drops a set whose pieces are the same items as an earlier
    /// one's, e.g. a "+1" series that maps onto its base series. Earlier sets win (dresser before model before mog station).
    /// </summary>
    public static IReadOnlyList<SetSubject> ResolveAll(IEnumerable<GlamourSet> sets, Func<uint, GlamourItem?> itemOf)
    {
        var seen = new HashSet<string>();
        var result = new List<SetSubject>();
        foreach (var set in sets)
        {
            if (Resolve(set, itemOf) is { } resolved && seen.Add(string.Join(",", resolved.Pieces.Select(p => p.Id).Order())))
                result.Add(resolved);
        }
        return result;
    }

    private static IReadOnlySet<uint> CommonJobs(IReadOnlyList<GlamourItem> pieces)
    {
        var armour = pieces.Where(p => Armour.Contains(p.Slot)).ToList();
        var source = armour.Count > 0 ? armour : pieces.ToList();
        var jobs = new HashSet<uint>(source.Count > 0 ? source[0].ClassJobIds : []);
        foreach (var piece in source.Skip(1))
            jobs.IntersectWith(piece.ClassJobIds);
        return jobs;
    }
}
