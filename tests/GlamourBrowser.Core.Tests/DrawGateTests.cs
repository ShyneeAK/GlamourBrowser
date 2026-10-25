using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class DrawGateTests
{
    [Fact]
    public void ReadyAfterChange_ThenStable_AndBlendDone()
    {
        var gate = new DrawGate(minFrames: 10, stableFrames: 2, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: true));   // before the draw
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponAsWanted: true));   // draw animation, blending
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponAsWanted: true));   // stable, but still blending
        Assert.Equal(PoseGateState.Ready, gate.Step(2, 1, weaponAsWanted: true));  // stable and blend done
    }

    [Fact]
    public void NoChangeSeen_ReadyOnlyAfterMinFrames()
    {
        var gate = new DrawGate(minFrames: 4, stableFrames: 1, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Ready, gate.Step(7, 1, weaponAsWanted: true));
    }

    [Fact]
    public void Timeout()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 1, timeoutFrames: 2);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 2, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 2, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Timeout, gate.Step(1, 2, weaponAsWanted: true));
    }

    [Fact]
    public void TileKey_WeaponDrawnIsPartOfKey()
    {
        var a = new TileKey(1, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose);
        Assert.False(a.WeaponDrawn);
        Assert.NotEqual(a, a with { WeaponDrawn = true });
    }

    [Fact]
    public void NotReady_WhileTheWeaponIsNotActuallyDrawn()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 0, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: false));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: false));
        Assert.Equal(PoseGateState.Ready, gate.Step(1, 1, weaponAsWanted: true));
    }

    [Fact]
    public void Timeout_WhileTheWeaponNeverGetsAsWanted()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 0, timeoutFrames: 3);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: false));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: false));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: false));
        Assert.Equal(PoseGateState.Timeout, gate.Step(1, 1, weaponAsWanted: false));
    }

    [Fact]
    public void StableCountRestarts_WhenTheTimelinesChangeAgain()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 2, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 1, weaponAsWanted: true));   // changed: stable from 0 again
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 1, weaponAsWanted: true));
        Assert.Equal(PoseGateState.Ready, gate.Step(2, 1, weaponAsWanted: true));
    }

    [Fact]
    public void Changed_OnlyOnceTheTimelinesDifferFromTheFirstLook()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 0, timeoutFrames: 100);
        gate.Step(5, 1, weaponAsWanted: false);
        gate.Step(5, 1, weaponAsWanted: false);
        Assert.False(gate.Changed);   // nothing started: the toggle did not take (yet)
        gate.Step(6, 1, weaponAsWanted: false);
        Assert.True(gate.Changed);
        gate.Step(5, 1, weaponAsWanted: false);
        Assert.True(gate.Changed);    // back to the first signature still counts as changed
    }
}
