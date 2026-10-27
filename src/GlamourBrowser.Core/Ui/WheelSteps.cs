namespace GlamourBrowser.Core.Ui;

/// <summary>
/// Turns mouse wheel deltas into whole steps: a notch of a wheel is one step, the small deltas of a touchpad or a
/// free-spinning wheel add up until they make one, instead of one step for each.
/// </summary>
public sealed class WheelSteps
{
    private float sum;

    /// <returns>The whole steps the delta completes; positive for a wheel turned up (back).</returns>
    public int Add(float delta)
    {
        // Frames without wheel input lie between the small deltas of a touchpad: they keep what has added up.
        if (delta == 0)
            return 0;
        // A turn the other way starts afresh: what was left over belonged to the other direction.
        if (sum != 0 && MathF.Sign(delta) != MathF.Sign(sum))
            sum = 0;
        sum += delta;
        var steps = (int)sum;
        sum -= steps;
        return steps;
    }

    /// <summary>Forgets what was left over (the mouse left the area).</summary>
    public void Reset() => sum = 0;
}
