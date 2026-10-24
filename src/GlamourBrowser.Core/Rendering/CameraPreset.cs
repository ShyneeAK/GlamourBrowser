namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Absolute state of a CharaView: the character's rotation and X/Y position in the view's scene,
/// and the camera distance. Captured from the live preview, applied after ResetPositions.
/// </summary>
public readonly record struct CameraPreset(float Rotation, float PositionX, float PositionY, float Distance);
