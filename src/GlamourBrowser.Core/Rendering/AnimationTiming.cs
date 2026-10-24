namespace GlamourBrowser.Core.Rendering;

public static class AnimationTiming
{
    private const float FrameTime = 1f / 30f;

    /// <summary>
    /// The last frame of an animation of <paramref name="duration"/> seconds: tiles freeze there, where an idle pose or
    /// the battle stance has fully settled. Exactly the duration would wrap back to the start.
    /// </summary>
    public static float LastFrame(float duration) => MathF.Max(0f, duration - FrameTime);
}
