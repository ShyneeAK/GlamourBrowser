namespace GlamourBrowser.Core.Rendering;

public enum PoseGateState
{
    Wait,
    Ready,
    Timeout,
}

/// <summary>
/// Holds a tile back until its character really plays the wanted idle-pose timeline, plus a few frames
/// for the blend from the previous animation; gives up after a timeout.
/// </summary>
/// <param name="wantedTimeline">0 means no pose wanted: ready at once, unless the override just changed.</param>
/// <param name="overrideChanged">The view had a different pose before: going back to the standard pose blends too.</param>
public sealed class PoseGate(ushort wantedTimeline, int blendFrames, int timeoutFrames, bool overrideChanged = false)
{
    private int frames;
    private int matched;

    public int Frames => frames;

    public PoseGateState Step(ushort currentBaseTimeline)
    {
        frames++;
        if (wantedTimeline == 0)
            return !overrideChanged || frames > blendFrames ? PoseGateState.Ready : PoseGateState.Wait;

        if (currentBaseTimeline == wantedTimeline && ++matched >= blendFrames)
            return PoseGateState.Ready;
        return frames > timeoutFrames ? PoseGateState.Timeout : PoseGateState.Wait;
    }
}
