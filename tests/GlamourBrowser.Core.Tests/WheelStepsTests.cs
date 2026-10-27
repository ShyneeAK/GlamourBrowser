using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class WheelStepsTests
{
    [Fact]
    public void ANotchIsOneStep()
    {
        var wheel = new WheelSteps();
        Assert.Equal(-1, wheel.Add(-1f));
        Assert.Equal(1, wheel.Add(1f));
    }

    [Fact]
    public void SmallDeltasAddUpToAStep()
    {
        var wheel = new WheelSteps();
        Assert.Equal(0, wheel.Add(-0.4f));
        Assert.Equal(0, wheel.Add(-0.4f));
        Assert.Equal(-1, wheel.Add(-0.4f));
        Assert.Equal(0, wheel.Add(-0.4f));
    }

    [Fact]
    public void ATurnTheOtherWayStartsAfresh()
    {
        var wheel = new WheelSteps();
        wheel.Add(-0.9f);
        Assert.Equal(0, wheel.Add(0.5f));
        Assert.Equal(1, wheel.Add(0.5f));
    }

    [Fact]
    public void ResetForgetsTheRest()
    {
        var wheel = new WheelSteps();
        wheel.Add(-0.9f);
        wheel.Reset();
        Assert.Equal(0, wheel.Add(-0.2f));
    }
}
