using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>A look is what a character wears: at most one piece per slot, with its dyes.</summary>
public static class Looks
{
    /// <summary>The basis with the pieces of <paramref name="over"/> in their slots; in slot order, empty items left out.</summary>
    public static IReadOnlyList<CustomPiece> Overlay(IReadOnlyList<CustomPiece> basis, IReadOnlyList<CustomPiece> over)
    {
        var bySlot = new Dictionary<EquipSlot, CustomPiece>();
        foreach (var piece in basis.Concat(over))
            bySlot[piece.Slot] = piece;
        return bySlot.Values.Where(p => p.ItemId != 0).OrderBy(p => p.Slot).ToList();
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
