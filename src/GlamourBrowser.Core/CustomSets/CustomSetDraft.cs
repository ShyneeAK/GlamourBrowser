using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>A custom set being edited: items go into their slots, dyes only into channels the item has.</summary>
public sealed class CustomSetDraft
{
    private readonly Dictionary<EquipSlot, CustomPiece> pieces;
    private readonly string savedName;
    private readonly IReadOnlyList<CustomPiece> savedPieces;
    // Pieces in slot order, built again after a change (the UI reads them every frame).
    private IReadOnlyList<CustomPiece>? sorted;

    // The slots an item blocks besides its own (GlamourItem.Blocks), for the pieces already in the draft.
    private readonly Func<uint, ushort> blocksOf;

    /// <param name="id">The stored set's id, null for a new set.</param>
    /// <param name="blocksOf">The slots an item blocks (<see cref="GlamourItem.Blocks"/>); without it no item blocks any.</param>
    public CustomSetDraft(uint? id, string name, IReadOnlyList<CustomPiece> pieces, Func<uint, ushort>? blocksOf = null)
    {
        this.blocksOf = blocksOf ?? (_ => 0);
        Id = id;
        Name = savedName = name;
        this.pieces = pieces.GroupBy(p => p.Slot).ToDictionary(g => g.Key, g => g.Last());
        savedPieces = Pieces;
    }

    public static CustomSetDraft From(CustomSet set, Func<uint, ushort>? blocksOf = null) => new(set.Id, set.Name, set.Pieces, blocksOf);

    public uint? Id { get; }

    public string Name { get; set; }

    /// <summary>In slot order; pieces whose item the catalog does not know stay too.</summary>
    public IReadOnlyList<CustomPiece> Pieces => sorted ??= pieces.Values.OrderBy(p => p.Slot).ToList();

    public CustomPiece? PieceIn(EquipSlot slot) => pieces.TryGetValue(slot, out var piece) ? piece : null;

    /// <summary>Name or pieces differ from what the draft started with.</summary>
    public bool IsDirty => Name != savedName || !Pieces.SequenceEqual(savedPieces);

    /// <summary>
    /// Puts the item into its slot. A ring goes into the selected ring slot, else the first free one (right, then
    /// left), else the right one. Dyes of the replaced piece stay for the channels the new item has; a channel the
    /// replaced piece had no dye on (or an empty slot) takes <paramref name="dye"/>. Like in the game, pieces in the
    /// slots the item blocks come off, and so does a piece that blocks the item's slot.
    /// </summary>
    /// <returns>The slot the item went into.</returns>
    public EquipSlot Put(GlamourItem item, EquipSlot? selectedSlot, DyePair dye = default)
    {
        var slot = item.Slot;
        if (slot is EquipSlot.RingRight or EquipSlot.RingLeft)
        {
            slot = selectedSlot is EquipSlot.RingRight or EquipSlot.RingLeft ? selectedSlot.Value
                : IsFree(EquipSlot.RingRight) ? EquipSlot.RingRight
                : IsFree(EquipSlot.RingLeft) ? EquipSlot.RingLeft
                : EquipSlot.RingRight;
        }

        var old = PieceIn(slot);
        var stain0 = old?.Stain0 is > 0 and var kept0 ? kept0 : dye.First;
        var stain1 = old?.Stain1 is > 0 and var kept1 ? kept1 : dye.Second;
        ClearBlocked(slot, item.Blocks);
        Set(new CustomPiece(slot, item.Id, item.DyeCount >= 1 ? stain0 : (byte)0, item.DyeCount >= 2 ? stain1 : (byte)0));
        return slot;
    }

    /// <summary>
    /// Takes every piece of the set in the slot the set wears it; other slots stay unless a piece blocks them. A custom
    /// set brings its dyes, a detected set's pieces are dyed like single items (<see cref="Put"/>) in <paramref name="dye"/>.
    /// </summary>
    public void PutAll(SetSubject set, DyePair dye = default)
    {
        foreach (var piece in set.Wear)
        {
            if (set.Set.Id.Kind != SetKind.Custom)
            {
                Put(piece, piece.Slot, dye);
                continue;
            }
            var (stain0, stain1) = set.StainsOf(piece.Slot);
            ClearBlocked(piece.Slot, piece.Blocks);
            Set(new CustomPiece(piece.Slot, piece.Id, stain0, stain1));
        }
    }

    /// <summary>Takes off what a new piece in <paramref name="slot"/> would collide with: pieces in the slots it blocks, and pieces that block its slot.</summary>
    private void ClearBlocked(EquipSlot slot, ushort blocks)
    {
        foreach (var piece in pieces.Values.ToList())
        {
            if (piece.Slot != slot && (SlotMask.Contains(blocks, piece.Slot) || SlotMask.Contains(blocksOf(piece.ItemId), slot)))
                Remove(piece.Slot);
        }
    }

    // A slot emptied on purpose (item 0, see TryOn.PutAll) is free as well.
    private bool IsFree(EquipSlot slot) => !pieces.TryGetValue(slot, out var piece) || piece.ItemId == 0;

    public void Remove(EquipSlot slot)
    {
        if (pieces.Remove(slot))
            sorted = null;
    }

    /// <summary>Sets dye channel 1 or 2 of the slot's piece; false when the slot is empty or the item has no such channel.</summary>
    public bool SetStain(EquipSlot slot, int channel, byte stain, int dyeCount)
    {
        if (channel is < 1 or > 2 || channel > dyeCount || PieceIn(slot) is not { } piece)
            return false;
        Set(channel == 1 ? piece with { Stain0 = stain } : piece with { Stain1 = stain });
        return true;
    }

    /// <summary>Puts the piece into its own slot as it is (used to take over a piece of another look).</summary>
    public void Set(CustomPiece piece)
    {
        pieces[piece.Slot] = piece;
        sorted = null;
    }

    /// <summary>The draft as a tile subject (id 0 while new), for the live preview; null without a known piece.</summary>
    public SetSubject? ToSubject(Func<uint, GlamourItem?> itemOf)
        => CustomSetSubjects.ToSubject(new CustomSet(Id ?? 0, Name, Pieces, default), itemOf);
}
