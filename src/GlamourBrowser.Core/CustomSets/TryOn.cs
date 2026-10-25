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
    private readonly CustomSetDraft clicked = new(null, "", []);
    private IReadOnlyList<CustomPiece>? pieces;

    public TryOn(IReadOnlyList<CustomPiece> basis) => Basis = basis.ToList();

    public IReadOnlyList<CustomPiece> Basis { get; private set; }

    /// <summary>The basis with every clicked slot replaced, in slot order.</summary>
    public IReadOnlyList<CustomPiece> Pieces => pieces ??= Looks.Overlay(Basis, clicked.Pieces);

    /// <summary>Something was clicked (or dyed) since the last reset.</summary>
    public bool HasChanges => clicked.Pieces.Count > 0;

    public event Action? Changed;

    public CustomPiece? PieceIn(EquipSlot slot) => Pieces.FirstOrDefault(p => p.Slot == slot) is { ItemId: not 0 } piece ? piece : null;

    /// <summary>The slot a clicked piece with the item is in, else one of the basis; null when it is not worn.</summary>
    public EquipSlot? SlotOf(uint itemId)
    {
        foreach (var piece in clicked.Pieces.Concat(Basis))
        {
            if (piece.ItemId == itemId)
                return piece.Slot;
        }
        return null;
    }

    /// <summary>Puts the item on, dyed in <paramref name="dye"/> on the channels it has (as its tile shows it).</summary>
    public void Put(GlamourItem item, DyePair dye)
    {
        clicked.Put(item, null);
        var slot = clicked.Pieces.Last(p => p.ItemId == item.Id).Slot;
        Dye(slot, item, dye);
        OnChanged();
    }

    /// <summary>Puts every piece of the set on: a custom set with its dyes, a detected one in <paramref name="dye"/>.</summary>
    public void PutAll(SetSubject set, DyePair dye)
    {
        clicked.PutAll(set);
        if (set.Set.Id.Kind != SetKind.Custom)
        {
            foreach (var piece in set.Wear)
                Dye(piece.Slot, piece, dye);
        }
        OnChanged();
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

    /// <summary>The look's hash without the slots a tile replaces anyway: its part of the tile key.</summary>
    public uint KeyHash(ushort exceptSlots) => Looks.Hash(Pieces, exceptSlots);

    private void Dye(EquipSlot slot, GlamourItem item, DyePair dye)
    {
        var (stain0, stain1) = TileDyes.For(item, dye);
        clicked.SetStain(slot, 1, stain0, item.DyeCount);
        clicked.SetStain(slot, 2, stain1, item.DyeCount);
    }

    private void OnChanged()
    {
        pieces = null;
        Changed?.Invoke();
    }
}
