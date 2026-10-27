using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileCoverTests
{
    private static ItemSubject Item(EquipSlot slot)
        => new(new GlamourItem(1, "Item", 0, slot, new HashSet<uint>(), 50, 0, RaceGenderRestriction.None));

    [Fact]
    public void WeaponsShown_OnlyTheTilesOwnSlots()
        => Assert.Equal(SlotMask.Of(EquipSlot.Head), LiveWeapons.TileCover(Item(EquipSlot.Head), weaponsHidden: false));

    [Fact]
    public void WeaponsHidden_AlsoTheWeaponSlots()
        => Assert.Equal((ushort)(SlotMask.Of(EquipSlot.Head) | LiveWeapons.WeaponSlots), LiveWeapons.TileCover(Item(EquipSlot.Head), weaponsHidden: true));

    [Fact]
    public void WeaponsHidden_AWeaponTileKeepsTheTryOnsOtherHand()
        => Assert.Equal(SlotMask.Of(EquipSlot.OffHand), LiveWeapons.TileCover(Item(EquipSlot.OffHand), weaponsHidden: true));
}
