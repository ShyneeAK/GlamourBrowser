using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class CameraPresetsTests
{
    [Fact]
    public void EverySlotHasDefault()
    {
        foreach (var slot in Enum.GetValues<EquipSlot>())
            Assert.True(CameraPresets.Defaults.ContainsKey(slot), slot.ToString());
    }

    [Fact]
    public void Resolve_OverrideWins()
    {
        var custom = new CameraPreset(1, 2, 3, 4, 5);
        var overrides = new Dictionary<EquipSlot, CameraPreset> { [EquipSlot.Head] = custom };

        Assert.Equal(custom, CameraPresets.Resolve(EquipSlot.Head, overrides));
        Assert.Equal(CameraPresets.Defaults[EquipSlot.Body], CameraPresets.Resolve(EquipSlot.Body, overrides));
        Assert.Equal(CameraPresets.Defaults[EquipSlot.Head], CameraPresets.Resolve(EquipSlot.Head, null));
    }
}
