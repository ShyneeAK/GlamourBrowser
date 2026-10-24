namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Holds a weapon tile back until drawing the weapon has finished. The base timeline does not change when a
/// weapon is drawn, so the gate watches all timeline slots (as one signature): once they have changed and then
/// stay the same, and only one body animation still carries weight (the blend is done), the stance has settled.
/// Without any change seen it waits at least <c>minFrames</c>, in case the draw finished before the first look.
/// </summary>
public sealed class DrawGate(int minFrames, int stableFrames, int timeoutFrames)
{
    private int frames;
    private int? first;
    private int last;
    private int stable;
    private bool changed;

    public int Frames => frames;

    /// <param name="signature">A hash over all timeline slots of the character.</param>
    /// <param name="activeBodyAnimations">Body animations that still carry weight.</param>
    /// <param name="weaponDrawn">The character really has the weapon in hand (Character.IsWeaponDrawn).</param>
    public PoseGateState Step(int signature, int activeBodyAnimations, bool weaponDrawn)
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

        if (weaponDrawn && (changed || frames >= minFrames) && stable >= stableFrames && activeBodyAnimations <= 1)
            return PoseGateState.Ready;
        return frames > timeoutFrames ? PoseGateState.Timeout : PoseGateState.Wait;
    }
}
