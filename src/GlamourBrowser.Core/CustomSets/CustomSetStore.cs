using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>One character's custom sets, the last changed first.</summary>
/// <param name="variants">"+N" variants mapped to their base item (see <see cref="ItemVariants"/>): a variant is stored as its base.</param>
public sealed class CustomSetStore(ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
{
    public const int MaxNameLength = 40;
    public const string DefaultName = "Neues Set";

    private readonly List<CustomSet> sets = new();

    public ulong ContentId { get; } = contentId;

    /// <summary>The id the next new set gets; ids of deleted sets are not given out again (a wish may still point at one).</summary>
    public uint NextId { get; private set; } = 1;

    public IReadOnlyList<CustomSet> Sets => sets;

    public event Action? Changed;

    public CustomSet? Find(uint id) => sets.Find(s => s.Id == id);

    public CustomSet Add(string name, IReadOnlyList<CustomPiece> pieces, DateTimeOffset now)
    {
        var set = Normalize(new CustomSet(NextId++, name, pieces, now));
        Insert(set);
        Changed?.Invoke();
        return set;
    }

    /// <summary>Replaces the set with the same id. Returns false (and stays quiet) for an unknown id or the same content.</summary>
    public bool Update(CustomSet set)
    {
        var old = Find(set.Id);
        set = Normalize(set);
        if (old == null || old.SameContent(set))
            return false;

        sets.Remove(old);
        Insert(set);
        Changed?.Invoke();
        return true;
    }

    public bool Remove(uint id)
    {
        if (sets.RemoveAll(s => s.Id == id) == 0)
            return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Ids below <paramref name="nextId"/> were given out before (in a file that broke): new sets get none of them.</summary>
    public void ReserveIdsBelow(uint nextId) => NextId = Math.Max(NextId, nextId);

    /// <summary>Takes a set from the file as it is (id and time kept), without <see cref="Changed"/>.</summary>
    public void Restore(CustomSet set, uint nextId)
    {
        set = Normalize(set);
        sets.RemoveAll(s => s.Id == set.Id);
        Insert(set);
        NextId = Math.Max(Math.Max(NextId, nextId), set.Id + 1);
    }

    private void Insert(CustomSet set)
    {
        var index = sets.FindIndex(s => s.Changed < set.Changed || s.Changed == set.Changed && s.Id < set.Id);
        sets.Insert(index < 0 ? sets.Count : index, set);
    }

    /// <summary>Base items for variants, one piece per slot (the last wins) in slot order, no empty items; a trimmed name.</summary>
    private CustomSet Normalize(CustomSet set)
    {
        var name = set.Name.Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength].TrimEnd();
        var pieces = set.Pieces
            .Where(p => p.ItemId != 0)
            .Select(p => variants != null && variants.TryGetValue(p.ItemId, out var baseId) ? p with { ItemId = baseId } : p)
            .GroupBy(p => p.Slot)
            .Select(g => g.Last())
            .OrderBy(p => p.Slot)
            .ToList();
        return set with { Name = name.Length == 0 ? DefaultName : name, Pieces = pieces };
    }
}
