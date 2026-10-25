using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Rendering;

/// <summary>Everything that changes how a tile looks.</summary>
/// <param name="Pose">The idle pose (C-Pose index) the character stands in, or <see cref="StandardPose"/>.</param>
/// <param name="WeaponDrawn">Weapon tiles rendered with the weapon drawn, in the battle stance.</param>
/// <param name="Set">The set of a set tile (then <paramref name="ItemId"/> is 0); default for item tiles.</param>
public readonly record struct TileKey(uint ItemId, byte Stain0, byte Stain1, TileMode Mode, ulong AppearanceHash, int RenderWidth, byte Pose,
                                     bool WeaponDrawn = false, SetId Set = default)
{
    public const byte StandardPose = 255;
}
