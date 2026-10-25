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
        var sets = SlotSelection.ToggleMode(All, BrowseMode.Sets);
        var wishlist = SlotSelection.ToggleMode(sets, BrowseMode.Wishlist);
        var mySets = SlotSelection.ToggleMode(wishlist, BrowseMode.MySets);
        Assert.Equal(0, memory.Switch(All, 7, sets));
        Assert.Equal(0, memory.Switch(sets, 3, wishlist));
        Assert.Equal(0, memory.Switch(wishlist, 1, mySets));
        Assert.Equal(1, memory.Switch(mySets, 5, SlotSelection.ToggleMode(mySets, BrowseMode.Wishlist)));
        Assert.Equal(3, memory.Switch(wishlist, 1, SlotSelection.ToggleMode(wishlist, BrowseMode.Sets)));
        Assert.Equal(7, memory.Switch(sets, 3, SlotSelection.ToggleMode(sets, BrowseMode.Sets)));
    }

    [Fact]
    public void Switch_ADyeChangeKeepsThePage()
        => Assert.Equal(5, new PageMemory().Switch(Head, 5, Head with { Dye = 81 }));

    [Fact]
    public void SameView_KeepsThePage()
        => Assert.Equal(5, new PageMemory().Switch(Head, 5, Head with { Sort = Head.Sort }));

    [Fact]
    public void OtherFilterChange_StartsAtTheFirstPage_AndForgetsTheOthers()
    {
        // Another search is another list: its first page, and pages remembered for the old list no longer apply.
        var memory = new PageMemory();
        memory.Switch(Head, 4, Body);
        var searched = Body with { SearchText = "robe" };
        Assert.Equal(0, memory.Switch(Body, 2, searched));
        Assert.Equal(0, memory.Switch(searched, 2, SlotSelection.SelectSlot(searched, EquipSlot.Head, additive: false)));
    }

    [Fact]
    public void Forget_DropsTheRememberedPages()
    {
        // Another grid size or sort order: the remembered page numbers point at other items.
        var memory = new PageMemory();
        memory.Switch(Head, 4, Body);
        memory.Forget();
        Assert.Equal(0, memory.Switch(Body, 2, Head));
    }
}
