using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Rendering;

/// <summary>The dyes pieces are rendered in: the tile dye ("Farbe" next to the job picker) on the channels an item has.</summary>
public static class TileDyes
{
    /// <returns>The dye on channel 1 and 2 as far as the item has them, 0 otherwise.</returns>
    public static (byte Stain0, byte Stain1) For(GlamourItem item, byte dye)
        => (item.DyeCount >= 1 ? dye : (byte)0, item.DyeCount >= 2 ? dye : (byte)0);

    /// <summary>A custom set keeps its own dyes; items and detected sets take the tile dye.</summary>
    public static (byte Stain0, byte Stain1) Of(TileSubject subject, GlamourItem piece, byte dye)
        => subject is SetSubject { Set.Id.Kind: SetKind.Custom } set ? set.StainsOf(piece.Slot) : For(piece, dye);
}
