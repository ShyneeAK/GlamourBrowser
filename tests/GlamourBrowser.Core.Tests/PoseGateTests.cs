using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class PoseGateTests
{
    [Fact]
    public void NoPoseWanted_ReadyAtOnce() => Assert.Equal(PoseGateState.Ready, new PoseGate(0, 3, 10).Step(0));

    [Fact]
    public void WaitsForTheTimeline_ThenForTheBlend()
    {
        var gate = new PoseGate(1055, blendFrames: 3, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(3));     // still the default idle
        Assert.Equal(PoseGateState.Wait, gate.Step(1055));  // blend 1
        Assert.Equal(PoseGateState.Wait, gate.Step(1055));  // blend 2
        Assert.Equal(PoseGateState.Ready, gate.Step(1055)); // blend 3
    }

    [Fact]
    public void TimelineNeverPlays_TimesOut()
    {
        var gate = new PoseGate(1055, blendFrames: 3, timeoutFrames: 2);
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Timeout, gate.Step(3));
    }
}
