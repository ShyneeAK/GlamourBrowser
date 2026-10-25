using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Rendering;

public static class TileKeys
{
    /// <summary>
    /// The key of a set tile: no single item and no key dye (a custom set carries its dyes per slot, its content hash is
    /// the <paramref name="variant"/>), and the weapon of a set always stays sheathed.
    /// </summary>
    public static TileKey ForSet(SetId set, TileMode mode, ulong appearanceHash, int renderWidth, byte idlePose, uint variant = 0)
        => new(0, 0, 0, mode, appearanceHash, renderWidth, idlePose, WeaponDrawn: false, Set: set, Variant: variant);
}
