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

    [Fact]
    public void BackToStandard_WaitsForTheBlend()
    {
        var gate = new PoseGate(0, blendFrames: 2, timeoutFrames: 100, overrideChanged: true);
        Assert.Equal(PoseGateState.Wait, gate.Step(1055));
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Ready, gate.Step(3));
    }

    [Fact]
    public void PoseAlreadyPlaying_NoBlend_ReadyAtOnce()
        => Assert.Equal(PoseGateState.Ready, new PoseGate(1055, blendFrames: 0, timeoutFrames: 100).Step(1055));

    [Fact]
    public void LastFrame_IsJustBeforeTheEnd()
    {
        Assert.Equal(2f - 1f / 30f, AnimationTiming.LastFrame(2f), 4);
        Assert.Equal(0f, AnimationTiming.LastFrame(0.01f));
        Assert.Equal(0f, AnimationTiming.LastFrame(0f));
    }

    [Theory]
    [InlineData(PoseGateState.Ready, PoseGateState.Ready, PoseGateState.Ready)]
    [InlineData(PoseGateState.Ready, PoseGateState.Wait, PoseGateState.Wait)]
    [InlineData(PoseGateState.Wait, PoseGateState.Ready, PoseGateState.Wait)]
    [InlineData(PoseGateState.Timeout, PoseGateState.Wait, PoseGateState.Wait)]      // the other gate may still finish
    [InlineData(PoseGateState.Timeout, PoseGateState.Ready, PoseGateState.Timeout)]
    [InlineData(PoseGateState.Ready, PoseGateState.Timeout, PoseGateState.Timeout)]
    public void Both_ReadyOnlyWhenBothAre_WaitsWhileEitherWaits(PoseGateState a, PoseGateState b, PoseGateState expected)
    {
        Assert.Equal(expected, PoseGateStates.Both(a, b));
    }
}
