using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class StanceJobTests
{
    // Gladiator 1 → Paladin 19, Marauder 3 → Warrior 21; Dark Knight 32 has no base class.
    private static uint ParentOf(uint job) => job switch { 19 => 1, 21 => 3, _ => job };

    [Fact]
    public void For_PrefersTheJobOverItsBaseClass()
        => Assert.Equal((byte)19, StanceJob.For(new HashSet<uint> { 1, 19 }, ParentOf));

    [Fact]
    public void For_AJobWithoutBaseClassCounts()
        => Assert.Equal((byte)32, StanceJob.For(new HashSet<uint> { 32 }, ParentOf));

    [Fact]
    public void For_OnlyABaseClass()
        => Assert.Equal((byte)3, StanceJob.For(new HashSet<uint> { 3 }, ParentOf));

    [Fact]
    public void For_SeveralJobsTakesTheLowestId()
        => Assert.Equal((byte)19, StanceJob.For(new HashSet<uint> { 1, 3, 21, 19 }, ParentOf));

    [Fact]
    public void For_NoJobIsNull()
        => Assert.Null(StanceJob.For(new HashSet<uint>(), ParentOf));

    private static GlamourItem Item(EquipSlot slot, params uint[] jobs)
        => new(1, "Item", 0, slot, jobs.ToHashSet(), 70, 0, RaceGenderRestriction.None);

    [Fact]
    public void ForItem_AMainHandWeaponTakesItsJob()
        => Assert.Equal((byte)21, StanceJob.ForItem(Item(EquipSlot.MainHand, 3, 21), ParentOf));

    [Fact]
    public void ForItem_AnythingElseKeepsTheOwnJob()
    {
        Assert.Null(StanceJob.ForItem(Item(EquipSlot.OffHand, 1, 19), ParentOf));
        Assert.Null(StanceJob.ForItem(Item(EquipSlot.Body, 3, 21), ParentOf));
    }
}
