using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class PageMemoryTests
{
    private static readonly FilterState All = new();
    private static readonly FilterState Head = SlotSelection.SelectSlot(All, EquipSlot.Head, additive: false);
    private static readonly FilterState Body = SlotSelection.SelectSlot(Head, EquipSlot.Body, additive: false);

    [Fact]
    public void SwitchingSlots_ComesBackToEachPage()
    {
        var memory = new PageMemory();
        Assert.Equal(0, memory.Switch(Head, 4, Body));
        Assert.Equal(4, memory.Switch(Body, 2, Head));
        Assert.Equal(2, memory.Switch(Head, 4, Body));
    }

    [Fact]
    public void ModesAndTheWholeCatalog_HaveTheirOwnPage()
    {
        var memory = new PageMemory();
        var sets = SlotSelection.ToggleSets(All);
        var wishlist = SlotSelection.ToggleWishlist(sets);
        Assert.Equal(0, memory.Switch(All, 7, sets));
        Assert.Equal(0, memory.Switch(sets, 3, wishlist));
        Assert.Equal(3, memory.Switch(wishlist, 1, SlotSelection.ToggleSets(wishlist)));
        Assert.Equal(7, memory.Switch(sets, 3, SlotSelection.ToggleSets(sets)));
    }

    [Fact]
    public void SameView_KeepsThePage()
        => Assert.Equal(5, new PageMemory().Switch(Head, 5, Head with { Sort = Head.Sort }));

    [Fact]
    public void OtherFilterChange_KeepsThePage_AndForgetsTheOthers()
    {
        // Pages remembered for another search would point into a different list.
        var memory = new PageMemory();
        memory.Switch(Head, 4, Body);
        var searched = Body with { SearchText = "robe" };
        Assert.Equal(2, memory.Switch(Body, 2, searched));
        Assert.Equal(0, memory.Switch(searched, 2, SlotSelection.SelectSlot(searched, EquipSlot.Head, additive: false)));
    }
}
