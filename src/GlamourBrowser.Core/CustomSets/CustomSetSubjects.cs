using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>Custom sets as tiles: their slots, a content hash for the tile key, and the set subject with dyes.</summary>
public static class CustomSetSubjects
{
    public static IReadOnlyList<EquipSlot> Slots { get; } =
    [
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet,
        EquipSlot.Ears, EquipSlot.Neck, EquipSlot.Wrists, EquipSlot.RingRight, EquipSlot.RingLeft,
    ];

    /// <summary>FNV-1a over slot, item and dyes of every piece in slot order; never 0 (0 marks detected sets).</summary>
    public static uint Hash(CustomSet set)
    {
        var hash = 2166136261u;
        void Mix(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
                hash = (hash ^ ((value >> shift) & 0xFF)) * 16777619u;
        }

        foreach (var piece in set.Pieces.OrderBy(p => p.Slot))
        {
            Mix((uint)piece.Slot);
            Mix(piece.ItemId);
            Mix((uint)(piece.Stain0 | piece.Stain1 << 8));
        }
        return hash == 0 ? 1 : hash;
    }

    /// <summary>
    /// The set with the pieces the catalog knows, their dyes and <see cref="Hash"/> as variant; a piece is worn in its
    /// set slot (a ring in the left one too). Null when no piece is known.
    /// </summary>
    public static SetSubject? ToSubject(CustomSet set, Func<uint, GlamourItem?> itemOf)
    {
        var known = set.Pieces
            .Select(p => (Piece: p, Item: itemOf(p.ItemId)))
            .Where(x => x.Item != null)
            .Select(x => (x.Piece, Item: x.Item!.Slot == x.Piece.Slot ? x.Item : x.Item with { Slot = x.Piece.Slot }))
            .ToList();
        if (known.Count == 0)
            return null;

        var glamourSet = new GlamourSet(new SetId(SetKind.Custom, set.Id), set.Name,
            known.Select(x => new SetPiece(x.Item.Id, x.Piece.Slot)).ToList(), known.Max(x => x.Item.LevelEquip));
        return new SetSubject(glamourSet, known.Select(x => x.Item).ToList())
        {
            Dyes = known.ToDictionary(x => x.Piece.Slot, x => (x.Piece.Stain0, x.Piece.Stain1)),
            Variant = Hash(set),
        };
    }
}
