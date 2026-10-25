using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class WeaponStanceTests
{
    private static ItemSubject Weapon(EquipSlot slot, params uint[] jobs)
        => new(new GlamourItem(1, "Item", 0, slot, jobs.ToHashSet(), 70, 0, RaceGenderRestriction.None));

    [Fact]
    public void Of_WeaponsForTheSameJobsShareAStance()
        => Assert.Equal(WeaponStance.Of(Weapon(EquipSlot.MainHand, 1, 19)), WeaponStance.Of(Weapon(EquipSlot.MainHand, 19, 1)));

    [Fact]
    public void Of_WeaponsForOtherJobsHaveAnotherStance()
        => Assert.NotEqual(WeaponStance.Of(Weapon(EquipSlot.MainHand, 1, 19)), WeaponStance.Of(Weapon(EquipSlot.MainHand, 37)));

    [Fact]
    public void Of_AnythingButAMainHandWeaponDrawsTheOwnWeapon()
    {
        Assert.Null(WeaponStance.Of(Weapon(EquipSlot.OffHand, 1, 19)));
        Assert.Null(WeaponStance.Of(Weapon(EquipSlot.Body, 1, 19)));
        Assert.Null(WeaponStance.Of(null));
    }
}
