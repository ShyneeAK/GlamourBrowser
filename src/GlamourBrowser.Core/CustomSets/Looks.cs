using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>A look is what a character wears: at most one piece per slot, with its dyes.</summary>
public static class Looks
{
    /// <summary>
    /// The basis with the pieces of <paramref name="over"/> in their slots; in slot order, empty items left out. Like in
    /// the game, a piece that blocks other slots (<paramref name="blocksOf"/>: a robe over legs and feet, a two-handed
    /// weapon over the off hand) hides the basis pieces there, and a basis piece that blocks a slot taken from above comes
    /// off. <paramref name="cover"/> (a <see cref="SlotMask"/>) is left free for pieces put on afterwards (a tile's
    /// items): pieces in it, and pieces that block one of its slots, are left out.
    /// </summary>
    public static IReadOnlyList<CustomPiece> Overlay(IReadOnlyList<CustomPiece> basis, IReadOnlyList<CustomPiece> over,
                                                     Func<uint, ushort>? blocksOf = null, ushort cover = 0)
    {
        ushort Blocks(CustomPiece piece) => blocksOf?.Invoke(piece.ItemId) ?? 0;

        var top = over.Where(p => !SlotMask.Contains(cover, p.Slot) && (Blocks(p) & cover) == 0).ToList();
        var taken = cover;
        foreach (var piece in top)
            taken |= (ushort)(SlotMask.Of(piece.Slot) | Blocks(piece));
        var kept = basis.Where(p => !SlotMask.Contains(taken, p.Slot) && (Blocks(p) & taken) == 0);
        return top.Concat(kept).Where(p => p.ItemId != 0).OrderBy(p => p.Slot).ToList();
    }

    /// <summary>FNV-1a over slot, item and dyes in slot order, without the slots in <paramref name="exceptSlots"/> (a <see cref="SlotMask"/>); never 0.</summary>
    public static uint Hash(IEnumerable<CustomPiece> pieces, ushort exceptSlots = 0)
    {
        var hash = 2166136261u;
        void Mix(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
                hash = (hash ^ ((value >> shift) & 0xFF)) * 16777619u;
        }

        foreach (var piece in pieces.Where(p => !SlotMask.Contains(exceptSlots, p.Slot)).OrderBy(p => p.Slot))
        {
            Mix((uint)piece.Slot);
            Mix(piece.ItemId);
            Mix((uint)(piece.Stain0 | piece.Stain1 << 8));
        }
        return hash == 0 ? 1 : hash;
    }
}
