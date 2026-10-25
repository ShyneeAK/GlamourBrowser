using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class CustomSetSubjectsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(1));

    private static GlamourItem Item(uint id, EquipSlot slot, int level = 50) => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), level, 2, RaceGenderRestriction.None);

    // Rings are always the right ring in the catalog.
    private static readonly Dictionary<uint, GlamourItem> Catalog = new()
    {
        [10] = Item(10, Body, 70), [20] = Item(20, Head, 90), [30] = Item(30, RingRight), [31] = Item(31, RingRight),
    };

    private static GlamourItem? ItemOf(uint id) => Catalog.GetValueOrDefault(id);

    private static CustomSet Set(params CustomPiece[] pieces) => new(5, "Mein Set", pieces, Now);

    [Fact]
    public void ToSubject_CarriesTheDyesPerSlot()
    {
        var subject = CustomSetSubjects.ToSubject(Set(new CustomPiece(Body, 10, 7, 9), new CustomPiece(Head, 20, 0, 0)), ItemOf)!;
        Assert.Equal(new SetId(SetKind.Custom, 5), subject.Set.Id);
        Assert.Equal("Mein Set", subject.Name);
        Assert.Equal(((byte)7, (byte)9), subject.StainsOf(Body));
        Assert.Equal(((byte)0, (byte)0), subject.StainsOf(Head));
        Assert.Equal(90, subject.Set.Level);
    }

    [Fact]
    public void ToSubject_OnePieceIsEnough() => Assert.Single(CustomSetSubjects.ToSubject(Set(new CustomPiece(Body, 10, 0, 0)), ItemOf)!.Pieces);

    [Fact]
    public void ToSubject_NoKnownPiece_IsNull() => Assert.Null(CustomSetSubjects.ToSubject(Set(new CustomPiece(Body, 4_000_000, 0, 0)), ItemOf));

    [Fact]
    public void ToSubject_DropsUnknownPieces()
        => Assert.Equal([10u], CustomSetSubjects.ToSubject(Set(new CustomPiece(Body, 10, 0, 0), new CustomPiece(Head, 4_000_000, 0, 0)), ItemOf)!.Pieces.Select(p => p.Id));

    [Fact]
    public void ToSubject_LeftRingIsWornLeft()
    {
        var subject = CustomSetSubjects.ToSubject(Set(new CustomPiece(RingRight, 30, 0, 0), new CustomPiece(RingLeft, 31, 0, 0)), ItemOf)!;
        Assert.Equal([(30u, RingRight), (31u, RingLeft)], subject.Wear.Select(p => (p.Id, p.Slot)));
    }

    [Fact]
    public void ToSubject_VariantIsTheHash()
    {
        var set = Set(new CustomPiece(Body, 10, 1, 0));
        Assert.Equal(CustomSetSubjects.Hash(set), CustomSetSubjects.ToSubject(set, ItemOf)!.Variant);
        Assert.NotEqual(0u, CustomSetSubjects.Hash(set));
    }

    [Fact]
    public void Hash_ChangesWithItemAndWithDye()
    {
        var hash = CustomSetSubjects.Hash(Set(new CustomPiece(Body, 10, 1, 0)));
        Assert.NotEqual(hash, CustomSetSubjects.Hash(Set(new CustomPiece(Body, 11, 1, 0))));
        Assert.NotEqual(hash, CustomSetSubjects.Hash(Set(new CustomPiece(Body, 10, 2, 0))));
        Assert.NotEqual(hash, CustomSetSubjects.Hash(Set(new CustomPiece(Body, 10, 1, 3))));
        Assert.NotEqual(hash, CustomSetSubjects.Hash(Set(new CustomPiece(Legs, 10, 1, 0))));
    }

    [Fact]
    public void Hash_IgnoresPieceOrder()
        => Assert.Equal(CustomSetSubjects.Hash(Set(new CustomPiece(Body, 10, 0, 0), new CustomPiece(Head, 20, 0, 0))),
                        CustomSetSubjects.Hash(Set(new CustomPiece(Head, 20, 0, 0), new CustomPiece(Body, 10, 0, 0))));

    [Fact]
    public void Slots_AreTheTwelveInSlotOrder()
        => Assert.Equal([MainHand, OffHand, Head, Body, Hands, Legs, Feet, Ears, Neck, Wrists, RingRight, RingLeft], CustomSetSubjects.Slots);

    [Fact]
    public void ForSet_KeysDifferByVariant()
    {
        var id = new SetId(SetKind.Custom, 5);
        Assert.NotEqual(TileKeys.ForSet(id, TileMode.CurrentOutfit, 1, 256, 0, 1), TileKeys.ForSet(id, TileMode.CurrentOutfit, 1, 256, 0, 2));
        Assert.Equal(0u, TileKeys.ForSet(id, TileMode.CurrentOutfit, 1, 256, 0).Variant);
    }
}
