using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Rendering;

/// <summary>Everything that changes how a tile looks.</summary>
/// <param name="Pose">The idle pose (C-Pose index) the character stands in, or <see cref="StandardPose"/>.</param>
/// <param name="WeaponDrawn">Weapon tiles rendered with the weapon drawn, in the battle stance.</param>
/// <param name="Set">The set of a set tile (then <paramref name="ItemId"/> is 0); default for item tiles.</param>
/// <param name="Variant">The content hash of a custom set (see <see cref="Items.SetSubject.Variant"/>), 0 otherwise.</param>
/// <param name="Dye">The tile dye a detected set's pieces render in (see <see cref="TileDyes"/>); items carry theirs as stains.</param>
/// <param name="LeftHand">A ring tile showing the ring on the left hand ("Ring links" in the slot bar).</param>
public readonly record struct TileKey(uint ItemId, byte Stain0, byte Stain1, TileMode Mode, ulong AppearanceHash, int RenderWidth, byte Pose,
                                     bool WeaponDrawn = false, SetId Set = default, uint Variant = 0, DyePair Dye = default,
                                     bool LeftHand = false)
{
    public const byte StandardPose = 255;
}
