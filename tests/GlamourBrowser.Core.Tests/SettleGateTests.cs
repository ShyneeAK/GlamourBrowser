using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class SettleGateTests
{
    [Fact]
    public void WaitsWhileLoading_ThenCopiesAfterSettleFrame()
    {
        var gate = new SettleGate(settleFrames: 1, timeoutFrames: 600, maxRetries: 30);
        Assert.Equal(SettleAction.Wait, gate.Step(idle: false, weaponsShown: true));
        Assert.Equal(SettleAction.Copy, gate.Step(idle: true, weaponsShown: true));
    }

    [Fact]
    public void HiddenWeapons_AreReappliedThenCopiedOnceShown()
    {
        var gate = new SettleGate(settleFrames: 1, timeoutFrames: 600, maxRetries: 30);
        Assert.Equal(SettleAction.Reapply, gate.Step(idle: true, weaponsShown: false));
        Assert.Equal(SettleAction.Copy, gate.Step(idle: true, weaponsShown: true));
    }

    [Fact]
    public void WeaponsNeverShown_CopiesAnywayAfterMaxRetries()
    {
        var gate = new SettleGate(settleFrames: 1, timeoutFrames: 600, maxRetries: 3);
        for (var i = 0; i < 3; i++)
            Assert.Equal(SettleAction.Reapply, gate.Step(idle: true, weaponsShown: false));
        Assert.Equal(SettleAction.CopyWithoutWeapons, gate.Step(idle: true, weaponsShown: false));
    }

    [Fact]
    public void NeverIdle_TimesOutEvenWithRetries()
    {
        var gate = new SettleGate(settleFrames: 1, timeoutFrames: 5, maxRetries: 30);
        var last = SettleAction.Wait;
        for (var i = 0; i < 6; i++)
            last = gate.Step(idle: i % 2 == 0, weaponsShown: false);
        Assert.Equal(SettleAction.Timeout, last);
    }
}
