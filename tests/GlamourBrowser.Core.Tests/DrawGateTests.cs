using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class DrawGateTests
{
    [Fact]
    public void ReadyAfterChange_ThenStable_AndBlendDone()
    {
        var gate = new DrawGate(minFrames: 10, stableFrames: 2, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponDrawn: true));   // before the draw
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponDrawn: true));   // draw animation, blending
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponDrawn: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(2, 2, weaponDrawn: true));   // stable, but still blending
        Assert.Equal(PoseGateState.Ready, gate.Step(2, 1, weaponDrawn: true));  // stable and blend done
    }

    [Fact]
    public void NoChangeSeen_ReadyOnlyAfterMinFrames()
    {
        var gate = new DrawGate(minFrames: 4, stableFrames: 1, timeoutFrames: 100);
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponDrawn: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponDrawn: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(7, 1, weaponDrawn: true));
        Assert.Equal(PoseGateState.Ready, gate.Step(7, 1, weaponDrawn: true));
    }

    [Fact]
    public void Timeout()
    {
        var gate = new DrawGate(minFrames: 1, stableFrames: 1, timeoutFrames: 2);
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 2, weaponDrawn: true));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 2, weaponDrawn: true));
        Assert.Equal(PoseGateState.Timeout, gate.Step(1, 2, weaponDrawn: true));
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
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponDrawn: false));
        Assert.Equal(PoseGateState.Wait, gate.Step(1, 1, weaponDrawn: false));
        Assert.Equal(PoseGateState.Ready, gate.Step(1, 1, weaponDrawn: true));
    }
}
