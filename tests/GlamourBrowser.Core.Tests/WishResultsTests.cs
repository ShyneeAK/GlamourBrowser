using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Wishlist;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class WishResultsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 14, 0, 0, TimeSpan.FromHours(2));
    private static readonly SetId Known = new(SetKind.Dresser, 500);

    private static GlamourItem Item(uint id, EquipSlot slot, string name) => new(id, name, 0, slot, new HashSet<uint>(), 70, 0, RaceGenderRestriction.None);

    private static readonly ItemSubject Robe = new(Item(1, Body, "Robe des Weisen"));
    private static readonly SetSubject Asuran = new(new GlamourSet(Known, "Asuran-Serie", [new SetPiece(2, Head), new SetPiece(3, Feet)], 60),
                                                    [Item(2, Head, "Asuran-Kappe"), Item(3, Feet, "Asuran-Stiefel")]);

    private static IReadOnlyList<TileSubject> Resolve(params WishEntry[] entries)
        => WishResults.Resolve(entries, id => id == 1 ? Robe : null, set => set == Known ? Asuran : null);

    [Fact]
    public void Resolve_NewestFirst_AndDropsUnknownEntries()
    {
        var subjects = Resolve(new WishEntry(WishKey.ForItem(1), Now), new WishEntry(WishKey.ForItem(99), Now),
                               new WishEntry(WishKey.ForSet(Known), Now), new WishEntry(WishKey.ForSet(new SetId(SetKind.Model, 7)), Now));
        Assert.Equal([Asuran, Robe], subjects);
    }

    [Fact]
    public void Filter_SearchMatchesTheNameOrAPiece()
    {
        IReadOnlyList<TileSubject> all = [Asuran, Robe];
        Assert.Equal([Asuran], WishResults.Filter(all, " stiefel ", hideDone: false, _ => false));
        Assert.Equal([Robe], WishResults.Filter(all, "weisen", hideDone: false, _ => false));
        Assert.Equal(all, WishResults.Filter(all, "", hideDone: false, _ => false));
    }

    [Fact]
    public void Filter_HideDone_DropsDoneEntries()
    {
        IReadOnlyList<TileSubject> all = [Asuran, Robe];
        // The set is only done with both pieces; one is not enough.
        Assert.Equal([Asuran], WishResults.Filter(all, "", hideDone: true, id => id is 1 or 2));
        Assert.Empty(WishResults.Filter(all, "", hideDone: true, _ => true));
        Assert.Equal(all, WishResults.Filter(all, "", hideDone: false, _ => true));
    }
}
