using GlamourBrowser.Core.CustomSets;
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

    private static readonly Dictionary<uint, GlamourItem> Catalog = new()
    {
        [1] = new GlamourItem(1, "Sword", 0, EquipSlot.MainHand, new HashSet<uint> { 1, 19 }, 70, 0, RaceGenderRestriction.None),
        [2] = new GlamourItem(2, "Other sword", 0, EquipSlot.MainHand, new HashSet<uint> { 19, 1 }, 70, 0, RaceGenderRestriction.None),
        [3] = new GlamourItem(3, "Gunblade", 0, EquipSlot.MainHand, new HashSet<uint> { 37 }, 70, 0, RaceGenderRestriction.None),
    };

    private static string? OfLook(params CustomPiece[] look) => WeaponStance.OfLook(look, id => Catalog.GetValueOrDefault(id));

    [Fact]
    public void OfLook_TakesTheMainHandsKind()
    {
        Assert.Equal(OfLook(new CustomPiece(EquipSlot.MainHand, 1, 0, 0)), OfLook(new CustomPiece(EquipSlot.MainHand, 2, 0, 0), new CustomPiece(EquipSlot.Head, 9, 0, 0)));
        Assert.NotEqual(OfLook(new CustomPiece(EquipSlot.MainHand, 1, 0, 0)), OfLook(new CustomPiece(EquipSlot.MainHand, 3, 0, 0)));
        Assert.Equal(WeaponStance.Of(new ItemSubject(Catalog[3])), OfLook(new CustomPiece(EquipSlot.MainHand, 3, 0, 0)));
    }

    [Fact]
    public void OfLook_AnUnknownWeaponIsItsOwnKind()
    {
        Assert.NotNull(OfLook(new CustomPiece(EquipSlot.MainHand, 77, 0, 0)));
        Assert.NotEqual(OfLook(new CustomPiece(EquipSlot.MainHand, 77, 0, 0)), OfLook(new CustomPiece(EquipSlot.MainHand, 78, 0, 0)));
    }

    [Fact]
    public void OfLook_NoMainHandIsNull()
        => Assert.Null(OfLook(new CustomPiece(EquipSlot.Head, 9, 0, 0)));
}
