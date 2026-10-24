namespace GlamourBrowser.Core.Rendering;

/// <summary>How the character stands on a tile: idle pose <see cref="Pose"/>, or with the weapon drawn.</summary>
public readonly record struct Stance(bool WeaponDrawn, byte Pose)
{
    /// <summary>Seconds into the animation as whole milliseconds, for <see cref="TileKey.MomentMs"/>.</summary>
    public static ushort ToMs(float seconds) => (ushort)Math.Clamp(MathF.Round(seconds * 1000f), 0f, ushort.MaxValue);
}
