namespace GlamourBrowser.Core.Rendering;

/// <summary>Camera deltas applied after CharaView.ResetPositions.</summary>
public readonly record struct CameraPreset(float Yaw, float Pitch, float Distance, float PanX, float PanY)
{
    public static CameraPreset Zero => default;
}
