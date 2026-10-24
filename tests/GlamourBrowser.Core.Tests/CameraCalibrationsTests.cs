using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class CameraCalibrationsTests
{
    private static readonly CameraPreset Head = new(0.5f, 0.1f, 1.2f, 2.0f);

    [Fact]
    public void Get_Missing_ReturnsNull()
    {
        var calibrations = new CameraCalibrations(new());
        Assert.Null(calibrations.Get(1, EquipSlot.Head));
    }

    [Fact]
    public void SetAndGet_PerCharacter()
    {
        var calibrations = new CameraCalibrations(new());
        calibrations.Set(1, EquipSlot.Head, Head);

        Assert.Equal(Head, calibrations.Get(1, EquipSlot.Head));
        Assert.Null(calibrations.Get(2, EquipSlot.Head));
    }

    [Fact]
    public void Remove_DeletesOnlyThatSlot()
    {
        var calibrations = new CameraCalibrations(new());
        calibrations.Set(1, EquipSlot.Head, Head);
        calibrations.Set(1, EquipSlot.Feet, Head);

        Assert.True(calibrations.Remove(1, EquipSlot.Head));
        Assert.Null(calibrations.Get(1, EquipSlot.Head));
        Assert.Equal(Head, calibrations.Get(1, EquipSlot.Feet));
        Assert.False(calibrations.Remove(1, EquipSlot.Head));
    }

    [Fact]
    public void RingLeft_SharesRingRight()
    {
        var calibrations = new CameraCalibrations(new());
        calibrations.Set(1, EquipSlot.RingLeft, Head);

        Assert.Equal(Head, calibrations.Get(1, EquipSlot.RingRight));
        Assert.Single(calibrations.For(1));
    }
}
