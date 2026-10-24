namespace GlamourBrowser.Core.Rendering;

/// <summary>How the character stands on a tile: idle pose <see cref="Pose"/>, or with the weapon drawn.</summary>
public readonly record struct Stance(bool WeaponDrawn, byte Pose);
