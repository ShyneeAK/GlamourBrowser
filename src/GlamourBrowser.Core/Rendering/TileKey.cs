namespace GlamourBrowser.Core.Rendering;

/// <summary>Everything that changes how a tile looks.</summary>
/// <param name="Pose">The idle pose (C-Pose index) the character stands in, or <see cref="StandardPose"/>.</param>
public readonly record struct TileKey(uint ItemId, byte Stain0, byte Stain1, TileMode Mode, ulong AppearanceHash, int RenderWidth, byte Pose)
{
    public const byte StandardPose = 255;
}
