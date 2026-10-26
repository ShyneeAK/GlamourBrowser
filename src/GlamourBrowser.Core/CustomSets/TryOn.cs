using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>
/// The live try-on: a basis (the worn outfit or nothing, after the tile mode) with the clicked pieces over it. Clicks
/// follow the set editor's rules (rings, dyes only on channels an item has); the basis can change under them.
/// </summary>
public sealed class TryOn
{
    private readonly CustomSetDraft clicked;
    private readonly Func<uint, ushort> blocksOf;
    // The look and its key hash per slots left free for a tile, asked for every tile on every frame; dropped on any change.
    private readonly Dictionary<ushort, (IReadOnlyList<CustomPiece> Look, uint Hash)> looks = new();

    /// <param name="blocksOf">The slots an item blocks (<see cref="GlamourItem.Blocks"/>), for the basis and the clicked pieces.</param>
    public TryOn(IReadOnlyList<CustomPiece> basis, Func<uint, ushort>? blocksOf = null)
    {
        this.blocksOf = blocksOf ?? (_ => 0);
        clicked = new CustomSetDraft(null, "", [], this.blocksOf);
        Basis = basis.ToList();
    }

    public IReadOnlyList<CustomPiece> Basis { get; private set; }

    /// <summary>
    /// The basis with every clicked slot replaced, in slot order; a clicked piece that blocks other slots (a robe over
    /// legs and feet) hides the basis there for as long as it is worn.
    /// </summary>
    public IReadOnlyList<CustomPiece> Pieces => LookFor(0);

    /// <summary>
    /// What a tile wears around its own items: <see cref="Pieces"/> without the slots in <paramref name="cover"/> (a
    /// <see cref="SlotMask"/>) and without pieces that block one of them, which the tile's items would take off.
    /// </summary>
    public IReadOnlyList<CustomPiece> LookFor(ushort cover) => Cached(cover).Look;

    /// <summary>Something was clicked (or dyed) since the last reset.</summary>
    public bool HasChanges => clicked.Pieces.Count > 0;

    public event Action? Changed;

    public CustomPiece? PieceIn(EquipSlot slot) => Pieces.FirstOrDefault(p => p.Slot == slot) is { ItemId: not 0 } piece ? piece : null;

    /// <summary>The slot a clicked piece with the item is in, else one of the look; null when it is not worn (or hidden).</summary>
    public EquipSlot? SlotOf(uint itemId)
    {
        foreach (var piece in clicked.Pieces.Concat(Pieces))
        {
            if (piece.ItemId == itemId)
                return piece.Slot;
        }
        return null;
    }

    /// <summary>
    /// Puts the item on, dyed in <paramref name="dye"/> on the channels it has (as its tile shows it). A ring goes on the
    /// hand in <paramref name="ring"/> when given; else one that is already worn stays on its finger, so clicking it (to
    /// see or dye it) does not push the other ring off.
    /// </summary>
    public void Put(GlamourItem item, DyePair dye, EquipSlot? ring = null)
    {
        var isRing = item.Slot is EquipSlot.RingRight or EquipSlot.RingLeft;
        var worn = !isRing ? null : ring is EquipSlot.RingRight or EquipSlot.RingLeft ? ring : SlotOf(item.Id);
        var slot = clicked.Put(item, worn is EquipSlot.RingRight or EquipSlot.RingLeft ? worn : null);
        Dye(slot, item, dye);
        OnChanged();
    }

    /// <summary>
    /// Puts every piece of the set on: a custom set with its dyes, a detected one in <paramref name="dye"/>. An own design
    /// is a whole look: the slots it leaves empty (weapons aside) are emptied, also of earlier clicks.
    /// </summary>
    public void PutAll(SetSubject set, DyePair dye)
    {
        clicked.PutAll(set);
        if (set.Set.Id.Kind == SetKind.Custom)
        {
            var worn = SlotMask.Of(set.Wear.Select(p => p.Slot));
            foreach (var slot in CustomSetSubjects.Slots.Where(s => SlotMask.Contains(CustomSetSubjects.LookSlots, s) && !SlotMask.Contains(worn, s)))
                clicked.Set(new CustomPiece(slot, 0, 0, 0));
        }
        else
        {
            foreach (var piece in set.Wear)
                Dye(piece.Slot, piece, dye);
        }
        OnChanged();
    }

    /// <summary>
    /// Empties the slot, also of the basis, until something is put on there again (the weapons too, as on a glamour
    /// plate). False when nothing was worn there.
    /// </summary>
    public bool TakeOff(EquipSlot slot)
    {
        if (PieceIn(slot) == null)
            return false;
        clicked.Set(new CustomPiece(slot, 0, 0, 0));
        OnChanged();
        return true;
    }

    /// <summary>Dyes channel 1 or 2 of a worn piece; a piece of the basis is taken over first. False for a channel the item lacks.</summary>
    public bool SetStain(EquipSlot slot, int channel, byte stain, int dyeCount)
    {
        if (channel is < 1 or > 2 || channel > dyeCount || PieceIn(slot) is not { } piece)
            return false;
        if (clicked.PieceIn(slot) == null)
            clicked.Set(piece);
        clicked.SetStain(slot, channel, stain, dyeCount);
        OnChanged();
        return true;
    }

    /// <summary>A new basis under the clicked pieces (the worn outfit changed); nothing happens when it is the same.</summary>
    public void SetBasis(IReadOnlyList<CustomPiece> basis)
    {
        if (basis.SequenceEqual(Basis))
            return;
        Basis = basis.ToList();
        OnChanged();
    }

    /// <summary>Back to only the (new) basis.</summary>
    public void Reset(IReadOnlyList<CustomPiece> basis)
    {
        foreach (var piece in clicked.Pieces.ToList())
            clicked.Remove(piece.Slot);
        Basis = basis.ToList();
        OnChanged();
    }

    /// <summary>The hash of <see cref="LookFor"/>: the try-on's part of the key of a tile that covers <paramref name="cover"/>.</summary>
    public uint KeyHash(ushort cover) => Cached(cover).Hash;

    private (IReadOnlyList<CustomPiece> Look, uint Hash) Cached(ushort cover)
    {
        if (!looks.TryGetValue(cover, out var cached))
        {
            var look = Looks.Overlay(Basis, clicked.Pieces, blocksOf, cover);
            looks[cover] = cached = (look, Looks.Hash(look));
        }
        return cached;
    }

    private void Dye(EquipSlot slot, GlamourItem item, DyePair dye)
    {
        var (stain0, stain1) = TileDyes.For(item, dye);
        clicked.SetStain(slot, 1, stain0, item.DyeCount);
        clicked.SetStain(slot, 2, stain1, item.DyeCount);
    }

    private void OnChanged()
    {
        looks.Clear();
        Changed?.Invoke();
    }
}
