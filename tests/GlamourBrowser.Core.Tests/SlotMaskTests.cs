using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class SlotMaskTests
{
    [Theory]
    [InlineData(EquipSlot.MainHand)]
    [InlineData(EquipSlot.Head)]
    [InlineData(EquipSlot.RingLeft)]
    public void Of_OneSlotIsTheMaskOfJustIt(EquipSlot slot)
        => Assert.Equal(SlotMask.Of(new[] { slot }), SlotMask.Of(slot));
}
