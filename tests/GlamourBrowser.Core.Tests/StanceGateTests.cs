using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class StanceGateTests
{
    [Fact]
    public void ReadyAfterChangeAndStable()
    {
        var gate = new StanceGate(stableFrames: 2, timeoutFrames: 100, expectChange: true);
        Assert.Equal(PoseGateState.Wait, gate.Step(3));    // idle, before the draw animation
        Assert.Equal(PoseGateState.Wait, gate.Step(900));  // draw animation
        Assert.Equal(PoseGateState.Wait, gate.Step(901));  // battle stance starts
        Assert.Equal(PoseGateState.Wait, gate.Step(901));
        Assert.Equal(PoseGateState.Ready, gate.Step(901));
    }

    [Fact]
    public void Timeout()
    {
        var gate = new StanceGate(stableFrames: 2, timeoutFrames: 3, expectChange: true);
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Wait, gate.Step(3));
        Assert.Equal(PoseGateState.Timeout, gate.Step(3));
    }

    [Fact]
    public void NoChangeExpected_ReadyWhenStable()
    {
        var gate = new StanceGate(stableFrames: 2, timeoutFrames: 100, expectChange: false);
        Assert.Equal(PoseGateState.Wait, gate.Step(901));
        Assert.Equal(PoseGateState.Wait, gate.Step(901));
        Assert.Equal(PoseGateState.Ready, gate.Step(901));
    }

    [Fact]
    public void TileKey_WeaponDrawnIsPartOfKey()
    {
        var a = new TileKey(1, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose, 0);
        Assert.False(a.WeaponDrawn);
        Assert.NotEqual(a, a with { WeaponDrawn = true });
    }
}
