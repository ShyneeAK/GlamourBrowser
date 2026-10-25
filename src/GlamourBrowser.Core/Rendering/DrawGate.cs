namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Holds a tile back until drawing or sheathing the weapon has finished. The base timeline does not change when a
/// weapon is drawn, so the gate watches all timeline slots (as one signature): once they have changed and then
/// stay the same, and only one body animation still carries weight (the blend is done), the stance has settled.
/// Without any change seen it waits at least <c>minFrames</c>, in case the toggle finished before the first look.
/// </summary>
public sealed class DrawGate(int minFrames, int stableFrames, int timeoutFrames)
{
    private int frames;
    private int? first;
    private int last;
    private int stable;
    private bool changed;

    public int Frames => frames;

    /// <summary>The timelines differed from the first look at some point: the draw or sheathe animation started.</summary>
    public bool Changed => changed;

    /// <param name="signature">A hash over all timeline slots of the character.</param>
    /// <param name="activeBodyAnimations">Body animations that still carry weight.</param>
    /// <param name="weaponAsWanted">The weapon really is where the tile wants it: in hand, or away from it.</param>
    public PoseGateState Step(int signature, int activeBodyAnimations, bool weaponAsWanted)
    {
        frames++;
        if (first == null)
        {
            first = last = signature;
        }
        else if (signature != last)
        {
            last = signature;
            stable = 0;
            changed |= signature != first;
        }
        else
        {
            stable++;
        }

        if (weaponAsWanted && (changed || frames >= minFrames) && stable >= stableFrames && activeBodyAnimations <= 1)
            return PoseGateState.Ready;
        return frames > timeoutFrames ? PoseGateState.Timeout : PoseGateState.Wait;
    }
}
