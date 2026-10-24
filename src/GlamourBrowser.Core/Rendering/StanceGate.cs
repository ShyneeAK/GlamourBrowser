namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Holds a weapon tile back until drawing the weapon has finished: the playing base timeline has changed
/// from the first one seen (the idle before the draw) and then stays the same for <c>stableFrames</c>.
/// </summary>
/// <param name="expectChange">False when the weapon was already drawn: only stability is waited for.</param>
public sealed class StanceGate(int stableFrames, int timeoutFrames, bool expectChange)
{
    private int frames;
    private ushort? first;
    private ushort last;
    private int stable;
    private bool changed = !expectChange;

    public PoseGateState Step(ushort playing)
    {
        frames++;
        if (first == null)
        {
            first = last = playing;
        }
        else if (playing != last)
        {
            last = playing;
            stable = 0;
            changed |= playing != first;
        }
        else if (changed)
        {
            stable++;
        }

        if (changed && stable >= stableFrames)
            return PoseGateState.Ready;
        return frames > timeoutFrames ? PoseGateState.Timeout : PoseGateState.Wait;
    }
}
