using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>A custom set being edited: items go into their slots, dyes only into channels the item has.</summary>
public sealed class CustomSetDraft
{
    private readonly Dictionary<EquipSlot, CustomPiece> pieces;
    private readonly string savedName;
    private readonly IReadOnlyList<CustomPiece> savedPieces;

    /// <param name="id">The stored set's id, null for a new set.</param>
    public CustomSetDraft(uint? id, string name, IReadOnlyList<CustomPiece> pieces)
    {
        Id = id;
        Name = savedName = name;
        this.pieces = pieces.GroupBy(p => p.Slot).ToDictionary(g => g.Key, g => g.Last());
        savedPieces = Pieces;
    }

    public static CustomSetDraft From(CustomSet set) => new(set.Id, set.Name, set.Pieces);

    public uint? Id { get; }

    public string Name { get; set; }

    /// <summary>In slot order; pieces whose item the catalog does not know stay too.</summary>
    public IReadOnlyList<CustomPiece> Pieces => pieces.Values.OrderBy(p => p.Slot).ToList();

    public CustomPiece? PieceIn(EquipSlot slot) => pieces.TryGetValue(slot, out var piece) ? piece : null;

    /// <summary>Name or pieces differ from what the draft started with.</summary>
    public bool IsDirty => Name != savedName || !Pieces.SequenceEqual(savedPieces);

    /// <summary>
    /// Puts the item into its slot. A ring goes into the selected ring slot, else the first free one (right, then
    /// left), else the right one. Dyes of the replaced piece stay for the channels the new item has.
    /// </summary>
    public void Put(GlamourItem item, EquipSlot? selectedSlot)
    {
        var slot = item.Slot;
        if (slot is EquipSlot.RingRight or EquipSlot.RingLeft)
        {
            slot = selectedSlot is EquipSlot.RingRight or EquipSlot.RingLeft ? selectedSlot.Value
                : !pieces.ContainsKey(EquipSlot.RingRight) ? EquipSlot.RingRight
                : !pieces.ContainsKey(EquipSlot.RingLeft) ? EquipSlot.RingLeft
                : EquipSlot.RingRight;
        }

        var old = PieceIn(slot);
        pieces[slot] = new CustomPiece(slot, item.Id, item.DyeCount >= 1 ? old?.Stain0 ?? 0 : (byte)0, item.DyeCount >= 2 ? old?.Stain1 ?? 0 : (byte)0);
    }

    /// <summary>Takes every piece of the set with its dyes, in the slot the set wears it; other slots stay.</summary>
    public void PutAll(SetSubject set)
    {
        foreach (var piece in set.Wear)
        {
            var (stain0, stain1) = set.StainsOf(piece.Slot);
            pieces[piece.Slot] = new CustomPiece(piece.Slot, piece.Id, stain0, stain1);
        }
    }

    public void Remove(EquipSlot slot) => pieces.Remove(slot);

    /// <summary>Sets dye channel 1 or 2 of the slot's piece; false when the slot is empty or the item has no such channel.</summary>
    public bool SetStain(EquipSlot slot, int channel, byte stain, int dyeCount)
    {
        if (channel is < 1 or > 2 || channel > dyeCount || PieceIn(slot) is not { } piece)
            return false;
        pieces[slot] = channel == 1 ? piece with { Stain0 = stain } : piece with { Stain1 = stain };
        return true;
    }

    /// <summary>The draft as a tile subject (id 0 while new), for the live preview; null without a known piece.</summary>
    public SetSubject? ToSubject(Func<uint, GlamourItem?> itemOf)
        => CustomSetSubjects.ToSubject(new CustomSet(Id ?? 0, Name, Pieces, default), itemOf);
}
